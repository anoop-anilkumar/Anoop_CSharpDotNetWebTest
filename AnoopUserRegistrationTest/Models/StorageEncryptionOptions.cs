namespace AnoopUserRegistrationTest.Models;

public class StorageEncryptionOptions
{
    public const string SectionName = "StorageEncryption";

    public string Key { get; set; } = string.Empty;
}
