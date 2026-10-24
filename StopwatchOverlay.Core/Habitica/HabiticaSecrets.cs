using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
namespace StopwatchOverlay.Habitica;

public interface IHabiticaSecretStore
{
    string? Read(string account);
    bool Write(string account, string token);
    void Delete(string account);
    string Status { get; }
}

/// <summary>Tokens are never saved as plaintext. Missing OS facilities fall back to this process only.</summary>
public sealed class HabiticaSecretStore : IHabiticaSecretStore
{
    public static HabiticaSecretStore Default { get; } = new();
    private readonly ConcurrentDictionary<string,string> session = new();
    private readonly ConcurrentDictionary<string,byte> suppressed = new();
    private readonly string directory;
    private readonly object gate = new();
    public string Status { get; private set; } = "Token not saved. OS-protected storage will be used when available; otherwise session only.";
    public HabiticaSecretStore(string? storageDirectory = null) => directory=storageDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StopwatchOverlay","HabiticaSecrets");
    private static bool Valid(string account) => Guid.TryParseExact(account,"D",out _);
    private string FileName(string account) => Path.Combine(directory,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(account.ToLowerInvariant())))+".bin");
    public string? Read(string account)
    {
        if(!Valid(account)) return null;
        account=account.ToLowerInvariant();
        lock(gate)
        {
            if(session.TryGetValue(account,out var value)) return value;
            if(suppressed.ContainsKey(account)) return null;
            try
            {
                if(OperatingSystem.IsWindows())
                {
                    var path=FileName(account); if(!File.Exists(path)) return null;
                    var bytes=Protect(File.ReadAllBytes(path),false);
                    try { Status="Windows account-protected token storage."; return Encoding.UTF8.GetString(bytes); }
                    finally { CryptographicOperations.ZeroMemory(bytes); }
                }
                if(OperatingSystem.IsLinux() && SecretTool("lookup",account,null,out var token)) { Status="Desktop secret service token storage."; return string.IsNullOrEmpty(token) ? null : token; }
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or CryptographicException or System.ComponentModel.Win32Exception) { }
            Status="Protected storage unavailable. Token is session-only; enter it again after restarting.";
            return null;
        }
    }
    public bool Write(string account,string token)
    {
        if(!Valid(account)||string.IsNullOrWhiteSpace(token)||token.Any(char.IsControl)) return false;
        account=account.ToLowerInvariant();
        lock(gate)
        {
            session[account]=token; suppressed.TryRemove(account,out _);
            try
            {
                if(OperatingSystem.IsWindows())
                {
                    Directory.CreateDirectory(directory);
                    var plain=Encoding.UTF8.GetBytes(token); byte[] protectedBytes;
                    try { protectedBytes=Protect(plain,true); } finally { CryptographicOperations.ZeroMemory(plain); }
                    var path=FileName(account); var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                    try { File.WriteAllBytes(temp,protectedBytes); File.Move(temp,path,true); }
                    finally { if(File.Exists(temp)) File.Delete(temp); }
                    Status="Windows account-protected token storage."; return true;
                }
                if(OperatingSystem.IsLinux()&&SecretTool("store",account,token,out _)) { Status="Desktop secret service token storage."; return true; }
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or CryptographicException or System.ComponentModel.Win32Exception) { }
            Status="Protected storage unavailable. Token is session-only; enter it again after restarting.";
            return true;
        }
    }
    public void Delete(string account)
    {
        if(!Valid(account)) return;
        account=account.ToLowerInvariant();
        lock(gate)
        {
            session.TryRemove(account,out _); suppressed[account]=0;
            try
            {
                if(OperatingSystem.IsWindows()) { File.Delete(FileName(account)); Status="Token removed."; return; }
                if(OperatingSystem.IsLinux()&&SecretTool("clear",account,null,out _)) { Status="Token removed."; return; }
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { }
            Status="Token removed for this session. Protected storage unavailable; a previously stored token may remain there.";
        }
    }
    private static bool SecretTool(string operation,string account,string? token,out string? output)
    {
        output=null;
        var start=new ProcessStartInfo("secret-tool") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true };
        start.ArgumentList.Add(operation);
        if(operation=="store") start.ArgumentList.Add("--label=Stopwatch Habitica API token");
        start.ArgumentList.Add("application"); start.ArgumentList.Add("StopwatchOverlay"); start.ArgumentList.Add("account"); start.ArgumentList.Add(account);
        using var process=Process.Start(start);
        if(process is null) return false;
        var stdout=process.StandardOutput.ReadToEndAsync(); var stderr=process.StandardError.ReadToEndAsync();
        if(token is not null) process.StandardInput.Write(token);
        process.StandardInput.Close();
        if(!process.WaitForExit(3000)) { try { process.Kill(true); } catch(InvalidOperationException) {} return false; }
        if(process.ExitCode!=0) return false;
        output=stdout.GetAwaiter().GetResult().TrimEnd('\r','\n');
        return true;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Blob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)] private static extern bool CryptProtectData(ref Blob input,string? description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("crypt32.dll",SetLastError=true)] private static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    private static byte[] Protect(byte[] value,bool encrypt)
    {
        var input=new Blob {Length=value.Length,Data=Marshal.AllocHGlobal(value.Length)}; Blob output=default;
        try
        {
            Marshal.Copy(value,0,input.Data,value.Length);
            var ok=encrypt ? CryptProtectData(ref input,null,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output) : CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
            if(!ok) throw new CryptographicException("Protected token storage failed.");
            var result=new byte[output.Length]; Marshal.Copy(output.Data,result,0,result.Length); return result;
        }
        finally
        {
            for(int i=0;i<input.Length;i++) Marshal.WriteByte(input.Data,i,0);
            Marshal.FreeHGlobal(input.Data);
            if(output.Data!=IntPtr.Zero) { for(int i=0;i<output.Length;i++) Marshal.WriteByte(output.Data,i,0); LocalFree(output.Data); }
        }
    }
}
