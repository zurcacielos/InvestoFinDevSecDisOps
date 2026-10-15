using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KeyVaultComparer.Api.Services.SecretStore
{
    public class WindowsCredentialStore : ISecureSecretStore
    {
        public string ProviderName => "WindowsCredentialManager";
        public string DisplayName => "Windows Credential Manager";
        public string Description => "Win32 Credential Store protected by Windows DPAPI/LSA (Windows 11)";
        public bool IsSupported => OperatingSystem.IsWindows();

        private const string TargetPrefix = "KeyVaultComparer:";
        private const int CRED_TYPE_GENERIC = 1;
        private const int CRED_PERSIST_LOCAL_MACHINE = 2;

        public Task<string?> GetSecretAsync(string key, CancellationToken ct = default)
        {
            if (!IsSupported) return Task.FromResult<string?>(null);

            string target = TargetPrefix + key;
            if (!CredRead(target, CRED_TYPE_GENERIC, 0, out IntPtr credPtr) || credPtr == IntPtr.Zero)
            {
                return Task.FromResult<string?>(null);
            }

            try
            {
                var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
                if (cred.CredentialBlobSize > 0 && cred.CredentialBlob != IntPtr.Zero)
                {
                    byte[] bytes = new byte[cred.CredentialBlobSize];
                    Marshal.Copy(cred.CredentialBlob, bytes, 0, cred.CredentialBlobSize);
                    return Task.FromResult<string?>(Encoding.UTF8.GetString(bytes));
                }
                return Task.FromResult<string?>(null);
            }
            finally
            {
                CredFree(credPtr);
            }
        }

        public Task SetSecretAsync(string key, string value, CancellationToken ct = default)
        {
            if (!IsSupported)
            {
                throw new PlatformNotSupportedException("Windows Credential Manager is only supported on Windows.");
            }

            string target = TargetPrefix + key;
            byte[] bytes = Encoding.UTF8.GetBytes(value);

            IntPtr blobPtr = Marshal.AllocHGlobal(bytes.Length);
            IntPtr targetPtr = Marshal.StringToHGlobalUni(target);
            IntPtr userPtr = Marshal.StringToHGlobalUni(Environment.UserName);

            try
            {
                Marshal.Copy(bytes, 0, blobPtr, bytes.Length);

                var cred = new CREDENTIAL
                {
                    Flags = 0,
                    Type = CRED_TYPE_GENERIC,
                    TargetName = targetPtr,
                    Comment = IntPtr.Zero,
                    CredentialBlobSize = bytes.Length,
                    CredentialBlob = blobPtr,
                    Persist = CRED_PERSIST_LOCAL_MACHINE,
                    AttributeCount = 0,
                    Attributes = IntPtr.Zero,
                    TargetAlias = IntPtr.Zero,
                    UserName = userPtr
                };

                if (!CredWrite(ref cred, 0))
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException($"Failed to write credential to Windows Credential Manager. Win32 Error: {err}");
                }

                return Task.CompletedTask;
            }
            finally
            {
                if (blobPtr != IntPtr.Zero) Marshal.FreeHGlobal(blobPtr);
                if (targetPtr != IntPtr.Zero) Marshal.FreeHGlobal(targetPtr);
                if (userPtr != IntPtr.Zero) Marshal.FreeHGlobal(userPtr);
            }
        }

        public Task DeleteSecretAsync(string key, CancellationToken ct = default)
        {
            if (!IsSupported) return Task.CompletedTask;

            string target = TargetPrefix + key;
            CredDelete(target, CRED_TYPE_GENERIC, 0);
            return Task.CompletedTask;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct CREDENTIAL
        {
            public int Flags;
            public int Type;
            public IntPtr TargetName;
            public IntPtr Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public int CredentialBlobSize;
            public IntPtr CredentialBlob;
            public int Persist;
            public int AttributeCount;
            public IntPtr Attributes;
            public IntPtr TargetAlias;
            public IntPtr UserName;
        }

        [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredWrite([In] ref CREDENTIAL userCredential, [In] uint flags);

        [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

        [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredDelete(string target, uint type, uint flags);

        [DllImport("Advapi32.dll", EntryPoint = "CredFree")]
        private static extern void CredFree([In] IntPtr cred);
    }
}
