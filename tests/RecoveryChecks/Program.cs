using Microsoft.AspNetCore.DataProtection;
var provider = DataProtectionProvider.Create(new DirectoryInfo(args[0]), b => b.SetApplicationName("ClubSphere"));
var clear = provider.CreateProtector("finance.bank").Unprotect(File.ReadAllText(args[1]));
if (!clear.Contains('\n') || clear.Length < 5) throw new Exception("Restored secret validation failed.");
Console.WriteLine("PASS copied key ring decrypts restored bank data on the same host; no plaintext emitted.");
