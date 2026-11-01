using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using NebriaChip.Vault.Win.Core;

namespace NebriaChip.Vault.Win;

/// <summary>PersistVaultAdapter 動作オプション</summary>
/// <param name="OnlySession">
/// セッション永続性を利用するか否か。
/// true の場合は書き込んだ資格情報はセッション中のみとなり、false の場合はローカルマシンでの永続化となる。
/// </param>
/// <param name="ThrowParseError">
/// オブジェクト読み出しメソッドでのパースエラー時に例外を送出するか否か。
/// true の場合は例外を送出し、false の場合は null (デフォルト値) の返却に変換する。
/// </param>
public record PersistOptions(bool OnlySession = false, bool ThrowParseError = false);

/// <summary>Windows 資格情報ストアへのアクセスアダプタ</summary>
public class PersistVaultAdapter
{
    // 構築
    #region コンストラクタ
    /// <summary>動作オプションを指定するコンストラクタ</summary>
    /// <param name="options">動作オプション</param>
    public PersistVaultAdapter(PersistOptions? options = null)
    {
        this.adapter = new();
        this.options = options == null ? null : options with { };
        this.writeOptions = new(
            Persist: options?.OnlySession == true ? CredPersist.Session : CredPersist.LocalMachine
        );
    }
    #endregion

    // 公開メソッド
    #region 書き込み
    /// <summary>汎用資格情報にバイナリデータを書き込む</summary>
    /// <param name="name">資格情報名称</param>
    /// <param name="data">書き込むデータ</param>
    /// <returns>書き込み成否</returns>
    public bool WriteGenericData(string name, ReadOnlySpan<byte> data)
        => this.adapter.Write(CredType.Generic, name, data, this.writeOptions);

    /// <summary>汎用資格情報にテキストを書き込む</summary>
    /// <param name="name">資格情報名称</param>
    /// <param name="text">書き込むテキスト</param>
    /// <returns>書き込み成否</returns>
    public bool WriteGenericText(string name, ReadOnlySpan<char> text)
    {
        var encoder = Encoding.UTF8.GetEncoder();
        var buffer = new ArrayBufferWriter<byte>();
        try
        {
            encoder.Convert(text, writer: buffer, flush: true, out var _, out var _);
            return this.adapter.Write(CredType.Generic, name, buffer.WrittenSpan, this.writeOptions);
        }
        finally
        {
            buffer.Clear();
        }
    }

    /// <summary>汎用資格情報にオブジェクトをシリアライズして書き込む</summary>
    /// <typeparam name="TObject">オブジェクトの型</typeparam>
    /// <param name="name">資格情報名称</param>
    /// <param name="obj">書き込むオブジェクトインスタンス</param>
    /// <param name="options">シリアライズオプション。主にコンバータを利用する目的。</param>
    /// <returns>書き込み成否</returns>
    [RequiresDynamicCode("This method uses the JsonSerializer.")]
    [RequiresUnreferencedCode("This method uses the JsonSerializer.")]
    public bool WriteGenericObject<TObject>(string name, TObject obj, JsonSerializerOptions? options = null)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        try
        {
            JsonSerializer.Serialize(writer, obj, options);
            writer.Flush();
            return this.adapter.Write(CredType.Generic, name, buffer.WrittenSpan, this.writeOptions);
        }
        finally
        {
            buffer.Clear();
        }
    }

    /// <summary>汎用資格情報にオブジェクトをシリアライズして書き込む</summary>
    /// <typeparam name="TObject">オブジェクトの型</typeparam>
    /// <param name="name">資格情報名称</param>
    /// <param name="obj">書き込むオブジェクトインスタンス</param>
    /// <param name="typeInfo">シリアライズメタ情報</param>
    /// <returns>書き込み成否</returns>
    public bool WriteGenericObject<TObject>(string name, TObject obj, JsonTypeInfo<TObject> typeInfo)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        try
        {
            JsonSerializer.Serialize(writer, obj, typeInfo);
            writer.Flush();
            return this.adapter.Write(CredType.Generic, name, buffer.WrittenSpan, this.writeOptions);
        }
        finally
        {
            buffer.Clear();
        }
    }
    #endregion

    #region 読み取り
    /// <summary>汎用資格情報からバイナリデータを読みだす</summary>
    /// <param name="name">資格情報名称</param>
    /// <returns>読みだしたデータ。失敗時は null を返却。</returns>
    public byte[]? ReadGenericData(string name)
        => this.adapter.Read(CredType.Generic, name);

    /// <summary>汎用資格情報からテキストデータを読みだす</summary>
    /// <param name="name">資格情報名称</param>
    /// <returns>読みだしたテキスト。失敗時は null を返却。</returns>
    public string? ReadGenericText(string name)
    {
        var data = this.adapter.Read(CredType.Generic, name);
        if (data == null) return null;
        return Encoding.UTF8.GetString(data);
    }

    /// <summary>汎用資格情報からデータを読み出してオブジェクトにデシリアライズする</summary>
    /// <typeparam name="TObject">オブジェクトの型</typeparam>
    /// <param name="name">資格情報名称</param>
    /// <param name="options">デシリアライズオプション。主にコンバータを利用する目的。</param>
    /// <returns>読みだしてデシリアライズしたインスタンス。読み出しの失敗時は null を返却。パースエラーの結果は動作オプションによる。</returns>
    [RequiresDynamicCode("This method uses the JsonSerializer.")]
    [RequiresUnreferencedCode("This method uses the JsonSerializer.")]
    public TObject? ReadGenericObject<TObject>(string name, JsonSerializerOptions? options = null)
    {
        var json = this.ReadGenericText(name);
        if (json == null) return default;

        try
        {
            return JsonSerializer.Deserialize<TObject>(json, options);
        }
        catch when (this.options?.ThrowParseError != true)
        {
            return default;
        }
    }

    /// <summary>汎用資格情報からデータを読み出してオブジェクトにデシリアライズする</summary>
    /// <typeparam name="TObject">オブジェクトの型</typeparam>
    /// <param name="name">資格情報名称</param>
    /// <param name="typeInfo">シリアライズメタ情報</param>
    /// <returns>読みだしてデシリアライズしたインスタンス。読み出しの失敗時は null を返却。パースエラーの結果は動作オプションによる。</returns>
    public TObject? ReadGenericObject<TObject>(string name, JsonTypeInfo<TObject> typeInfo)
    {
        var json = this.ReadGenericText(name);
        if (json == null) return default;

        try
        {
            return JsonSerializer.Deserialize(json, typeInfo);
        }
        catch when (this.options?.ThrowParseError != true)
        {
            return default;
        }

    }
    #endregion

    #region 削除
    /// <summary>資格情報を削除する</summary>
    /// <param name="name">資格情報名称</param>
    /// <returns>削除成否</returns>
    public bool DeleteGeneric(string name)
        => this.adapter.Delete(CredType.Generic, name);
    #endregion

    // 非公開フィールド
    #region 設定
    /// <summary>動作オプション</summary>
    private PersistOptions? options;
    #endregion

    #region バックエンド
    /// <summary>バックエンドアダプタ</summary>
    private WinVaultAdapter adapter;

    /// <summary>書き込みオプション</summary>
    private WriteOptions writeOptions;
    #endregion
}
