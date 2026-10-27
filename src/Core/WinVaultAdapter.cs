using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Security.Credentials;

namespace NebriaChip.Vault.Win.Core;

/// <summary>資格情報種別</summary>
public enum CredType
{
    /// <summary>不明</summary>
    Unknown = -1,
    /// <summary>汎用</summary>
    Generic,
    /// <summary>汎用証明書</summary>
    GenericCertificate,
    /// <summary>ドメインパスワード</summary>
    DomainPassword,
    /// <summary>ドメイン証明書</summary>
    DomainCertificate,
    /// <summary>ドメイン証明書</summary>
    DomainExtended,
}

/// <summary>資格情報の永続性種別</summary>
public enum CredPersist
{
    /// <summary>不明</summary>
    Unknown = -1,
    /// <summary>ログオン セッション中。ログオフにより消滅する。</summary>
    Session,
    /// <summary>ローカルマシン上。</summary>
    LocalMachine,
    /// <summary>ローミングプロファイル上。</summary>
    EnterpriseRoaming,
}

/// <summary>資格情報書き込みオプション</summary>
/// <param name="Comment">コメント</param>
/// <param name="UserName">ユーザ名。Generic では無視される</param>
/// <param name="TargetAlias">エイリアス名。Generic では無視される。</param>
/// <param name="Persist">資格情報の永続性</param>
/// <param name="Attributes">資格情報に関連付けられた属性情報</param>
public record WriteOptions(
    string? Comment = null,
    string? UserName = null,
    string? TargetAlias = null,
    CredPersist Persist = CredPersist.LocalMachine,
    IDictionary<string, byte[]>? Attributes = null
);

/// <summary>資格情報の読み取り追加情報</summary>
/// <param name="Comment">コメント</param>
/// <param name="UserName">ユーザ名</param>
/// <param name="TargetAlias">エイリアス名</param>
/// <param name="Persist">資格情報の永続性</param>
/// <param name="Attributes">資格情報に関連付けられた属性情報</param>
public record ReadExtra(
    string? Comment,
    string? UserName,
    string? TargetAlias,
    CredPersist Persist,
    IDictionary<string, byte[]> Attributes
);

/// <summary>秘密情報アイテム</summary>
/// <param name="Type">資格情報種別</param>
/// <param name="Name">資格情報名称</param>
/// <param name="Blob">秘密データ</param>
/// <param name="Extra">追加情報</param>
public record CredItem(CredType Type, string Name, byte[] Blob, ReadExtra Extra);


/// <summary>Windows 資格情報ストアへのアクセスアダプタ</summary>
public class WinVaultAdapter
{
    // 公開プロパティ
    #region 状態情報
    /// <summary>最後のAPI呼び出しエラー</summary>
    public int LastError { get; private set; }
    #endregion

    // 公開メソッド
    #region 書き込み
    /// <summary>資格情報を書き込む</summary>
    /// <param name="type">資格情報種別</param>
    /// <param name="name">資格情報名称</param>
    /// <param name="data">資格情報として格納する秘密データ</param>
    /// <param name="options">オプション情報</param>
    /// <returns>書き込み成否</returns>
    public bool Write(CredType type, string name, ReadOnlySpan<byte> data, WriteOptions? options = null)
        => this.writeCredCore(type, name, data, blob: true, options);

    /// <summary>資格情報のデータを維持して更新する</summary>
    /// <param name="type">資格情報種別</param>
    /// <param name="name">資格情報名称</param>
    /// <param name="options">オプション情報(更新情報)</param>
    /// <returns>書き込み成否</returns>
    public bool Write(CredType type, string name, WriteOptions options)
        => this.writeCredCore(type, name, ReadOnlySpan<byte>.Empty, blob: false, options);
    #endregion

    #region 読み取り
    /// <summary>資格情報を読み取る</summary>
    /// <param name="type">資格情報種別</param>
    /// <param name="name">資格情報名称</param>
    /// <returns>読み取った秘密データ</returns>
    public byte[]? Read(CredType type, string name)
        => this.readCredCore(type, name, withExtra: false, out var extra);

    /// <summary>資格情報と追加情報を読み取る</summary>
    /// <param name="type">資格情報種別</param>
    /// <param name="name">資格情報名称</param>
    /// <param name="extra">読み取った追加情報。返却値が成功を示す(nullでない)場合に有効となる。</param>
    /// <returns>読み取った秘密データ。失敗時には null となる。</returns>
    public byte[]? Read(CredType type, string name, out ReadExtra? extra)
        => this.readCredCore(type, name, withExtra: true, out extra);

    /// <summary>資格情報と追加情報を読み取る</summary>
    /// <remarks>このメソッドの動作は <see cref="Read(CredType, string, out ReadExtra?)"/> と同一で、メソッドシグネチャのみが異なる。</remarks>
    /// <param name="type">資格情報種別</param>
    /// <param name="name">資格情報名称</param>
    /// <param name="data">読み取った秘密データ。返却値が成功を示す場合に有効となる。</param>
    /// <param name="extra">読み取った追加情報。返却値が成功を示す場合に有効となる。</param>
    /// <returns>読み取り成否</returns>
    public bool TryRead(CredType type, string name, [NotNullWhen(true)] out byte[]? data, [NotNullWhen(true)] out ReadExtra? extra)
    {
        data = this.readCredCore(type, name, withExtra: true, out extra);
        return data != null;
    }
    #endregion

    #region 削除
    /// <summary>資格情報を削除する</summary>
    /// <param name="type">資格情報種別</param>
    /// <param name="name">資格情報名称</param>
    /// <returns>削除成否</returns>
    public bool Delete(CredType type, string name)
        => this.deleteCredCode(type, name);
    #endregion

    #region 列挙
    /// <summary>資格情報を列挙する</summary>
    /// <param name="filter">フィルタテキスト</param>
    /// <returns>読みだした資格情報リスト</returns>
    public IReadOnlyList<CredItem>? Enumerate(string? filter = null)
        => this.enumerateCredCode(filter);
    #endregion

    // 非公開メソッド
    #region 資格情報アクセス
    /// <summary>資格情報を書き込む</summary>
    /// <param name="type">資格情報種別</param>
    /// <param name="name">資格情報名称</param>
    /// <param name="data">秘密データ</param>
    /// <param name="blob">秘密データの更新を行うか否か</param>
    /// <param name="options">オプション情報</param>
    /// <returns>書き込み成否</returns>
    private unsafe bool writeCredCore(CredType type, string name, ReadOnlySpan<byte> data, bool blob, WriteOptions? options)
    {
        // マネージオブジェクトのピン止め管理
        using var pins = new PinContext();

        // 秘密データをピン止め 
        fixed (byte* dataPtr = data)
        {
            // 書き込み用データ作成
            var credential = new CREDENTIALW();
            credential.Type = type.ToApiType();
            credential.TargetName = (char*)pins.Add(name);
            credential.CredentialBlobSize = (uint)data.Length;
            credential.CredentialBlob = dataPtr;
            credential.Persist = CRED_PERSIST.CRED_PERSIST_LOCAL_MACHINE;   // デフォルト値

            // オプション情報があれば設定
            if (options != null)
            {
                // 各オプション情報を設定
                credential.Comment = (char*)pins.Add(options.Comment);
                credential.UserName = (char*)pins.Add(options.UserName);
                credential.TargetAlias = (char*)pins.Add(options.TargetAlias);
                credential.Persist = options.Persist switch
                {
                    CredPersist.Session => CRED_PERSIST.CRED_PERSIST_SESSION,
                    CredPersist.LocalMachine => CRED_PERSIST.CRED_PERSIST_LOCAL_MACHINE,
                    CredPersist.EnterpriseRoaming => CRED_PERSIST.CRED_PERSIST_ENTERPRISE,
                    _ => throw new ArgumentException($"{nameof(WriteOptions)}.{nameof(WriteOptions.Persist)}"),
                };

                // 属性情報があれば設定
                if (options.Attributes != null && 0 < options.Attributes.Count)
                {
                    // 属性情報を作成
                    var attributes = new CREDENTIAL_ATTRIBUTEW[options.Attributes.Count];
                    var attrIdx = 0;
                    foreach (var attrEntry in options.Attributes)
                    {
                        var attr = new CREDENTIAL_ATTRIBUTEW();
                        attr.Keyword = (char*)pins.Add(attrEntry.Key);
                        attr.Value = (byte*)pins.Add(attrEntry.Value);
                        attr.ValueSize = (uint)attrEntry.Value.Length;

                        attributes[attrIdx++] = attr;
                    }

                    // 書き込みデータに設定
                    credential.AttributeCount = (uint)attributes.Length;
                    credential.Attributes = (CREDENTIAL_ATTRIBUTEW*)pins.Add(attributes);
                }
            }

            // 書き込み
            var flag = blob ? 0 : PInvokeExtensions.CRED_PRESERVE_CREDENTIAL_BLOB;
            var result = PInvoke.CredWrite(in credential, flag);
            this.LastError = Marshal.GetLastPInvokeError();

            return result;
        }
    }

    /// <summary>資格情報を読み取る</summary>
    /// <param name="type">資格情報種別</param>
    /// <param name="name">資格情報名称</param>
    /// <param name="withExtra">追加情報を返却するか否か</param>
    /// <param name="extra">追加情報</param>
    /// <returns>読み取った秘密データ</returns>
    private unsafe byte[]? readCredCore(CredType type, string name, bool withExtra, out ReadExtra? extra)
    {
        var credPtr = default(CREDENTIALW*);
        try
        {
            // 資格情報の読み取り
            var result = PInvoke.CredRead(name, type.ToApiType(), out credPtr);
            this.LastError = Marshal.GetLastPInvokeError();

            // 追加情報の出力を初期化
            extra = null;

            // 視覚情報の読み取りが失敗していたら終える
            if (!result) return null;

            // APIデータをデコードする
            var decoded = decodeCred(credPtr, withExtra);
            extra = decoded.extra;

            return decoded.blob;
        }
        finally
        {
            // 取得したデータがある場合、開放する
            if (credPtr != null)
            {
                PInvoke.CredFree(credPtr);
            }
        }
    }

    /// <summary>資格情報を削除する</summary>
    /// <param name="type">資格情報種別</param>
    /// <param name="name">資格情報名称</param>
    /// <returns>削除成否</returns>
    private bool deleteCredCode(CredType type, string name)
    {
        var credType = type.ToApiType();
        var result = PInvoke.CredDelete(name, credType);
        this.LastError = Marshal.GetLastPInvokeError();
        return result;
    }

    /// <summary>資格情報を列挙する</summary>
    /// <param name="filter">フィルタテキスト</param>
    /// <returns>読みだした資格情報リスト</returns>
    private unsafe IReadOnlyList<CredItem>? enumerateCredCode(string? filter)
    {
        var credArrayPtr = default(CREDENTIALW**);
        try
        {
            // 資格情報の読み取り
            var result = PInvoke.CredEnumerate(filter, out var count, out credArrayPtr);
            this.LastError = Marshal.GetLastPInvokeError();

            // 視覚情報の読み取りが失敗していたら終える
            if (!result) return null;

            // 読み出した情報を変換
            var items = new List<CredItem>();
            for (var i = 0; i < count; i++)
            {
                var credPtr = credArrayPtr[i];

                // APIデータをデコードする
                var decoded = decodeCred(credPtr, withExtra: true);
                if (decoded.extra == null) continue;

                // デコードした結果を収集
                var type = credPtr->Type.DecodeType();
                var name = credPtr->TargetName.ToString();
                items.Add(new(type, name, decoded.blob, decoded.extra));
            }

            return items.ToArray();
        }
        finally
        {
            // 取得したデータがある場合、開放する
            if (credArrayPtr != null)
            {
                PInvoke.CredFree(credArrayPtr);
            }
        }
    }
    #endregion

    #region 変化
    /// <summary>APIで読みだした資格情報データをデコードする</summary>
    /// <param name="credPtr">デコード対象の</param>
    /// <param name="withExtra">追加情報をデコードするか否か</param>
    /// <returns>デコード結果の秘密データと追加情報</returns>
    private unsafe (byte[] blob, ReadExtra? extra) decodeCred(CREDENTIALW* credPtr, bool withExtra)
    {
        // 読み取った秘密データをコピー
        var blobSpan = new ReadOnlySpan<byte>(credPtr->CredentialBlob, (int)credPtr->CredentialBlobSize);
        var blob = blobSpan.ToArray();

        // 追加情報の取得が指定されている場合、返却用データを作る
        var extra = default(ReadExtra);
        if (withExtra)
        {
            // 読み取った属性データの変換
            var attributes = new Dictionary<string, byte[]>();
            for (var i = 0; i < credPtr->AttributeCount; i++)
            {
                var attr = credPtr->Attributes[i];
                var key = attr.Keyword.ToString();
                var value = new ReadOnlySpan<byte>(attr.Value, (int)attr.ValueSize).ToArray();
                if (key == null) continue;
                attributes.Add(key, value);
            }

            // 追加情報の変換
            extra = new(
                UserName: credPtr->UserName.ToString(),
                Comment: credPtr->Comment.ToString(),
                TargetAlias: credPtr->TargetAlias.ToString(),
                Persist: credPtr->Persist switch
                {
                    CRED_PERSIST.CRED_PERSIST_SESSION => CredPersist.Session,
                    CRED_PERSIST.CRED_PERSIST_LOCAL_MACHINE => CredPersist.LocalMachine,
                    CRED_PERSIST.CRED_PERSIST_ENTERPRISE => CredPersist.EnterpriseRoaming,
                    _ => CredPersist.Unknown,
                },
                Attributes: attributes
            );
        }

        return (blob, extra);
    }
    #endregion
}

/// <summary>P/Invoke 関連拡張データ</summary>
file static class PInvokeExtensions
{
    /// <summary>更新データフラグ：BLOBを維持した更新</summary>
    public const uint CRED_PRESERVE_CREDENTIAL_BLOB = 0x00000001;

    /// <summary>資格情報種別をAPI用の値に変換する</summary>
    /// <param name="self">資格情報種別</param>
    /// <returns>API用資格情報種別</returns>
    public static CRED_TYPE ToApiType(this CredType self)
        => self switch
        {
            CredType.Generic => CRED_TYPE.CRED_TYPE_GENERIC,
            CredType.GenericCertificate => CRED_TYPE.CRED_TYPE_GENERIC_CERTIFICATE,
            CredType.DomainPassword => CRED_TYPE.CRED_TYPE_DOMAIN_PASSWORD,
            CredType.DomainCertificate => CRED_TYPE.CRED_TYPE_DOMAIN_CERTIFICATE,
            CredType.DomainExtended => CRED_TYPE.CRED_TYPE_DOMAIN_EXTENDED,
            _ => throw new ArgumentException(nameof(CredType)),
        };


    public static CredType DecodeType(this CRED_TYPE self)
        => self switch
        {
            CRED_TYPE.CRED_TYPE_GENERIC => CredType.Generic,
            CRED_TYPE.CRED_TYPE_GENERIC_CERTIFICATE => CredType.GenericCertificate,
            CRED_TYPE.CRED_TYPE_DOMAIN_PASSWORD => CredType.DomainPassword,
            CRED_TYPE.CRED_TYPE_DOMAIN_CERTIFICATE => CredType.DomainCertificate,
            CRED_TYPE.CRED_TYPE_DOMAIN_EXTENDED => CredType.DomainExtended,
            _ => CredType.Unknown,
        };
}

