﻿using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace ParquetSharp
{
    /// <summary>
    /// Builder pattern for creating and configuring a <see cref="FileDecryptionProperties"/> object.
    /// Provides a fluent API for setting the decryption properties (footer key, encryption algorithm, etc.) for a Parquet file.
    /// </summary>
    public sealed class FileDecryptionPropertiesBuilder : IDisposable
    {
        /// <summary>
        /// Create a new <see cref="FileDecryptionPropertiesBuilder"/>.
        /// </summary>
        public FileDecryptionPropertiesBuilder()
        {
            ExceptionInfo.Check(FileDecryptionPropertiesBuilder_Create(out var handle));
            _handle = new ParquetHandle(handle, FileDecryptionPropertiesBuilder_Free);
        }

        public void Dispose()
        {
            _handle.Dispose();
        }

        /// <summary>
        /// Set the footer key used to decrypt the file.
        /// </summary>
        /// <param name="footerKey">An array of bytes used to decrypt the footer.</param>
        /// <returns>This builder instance.</returns>
        public FileDecryptionPropertiesBuilder FooterKey(byte[] footerKey)
        {
            var footerAesKey = new AesKey(footerKey);
            ExceptionInfo.Check(FileDecryptionPropertiesBuilder_Footer_Key(_handle.IntPtr, in footerAesKey));
            GC.KeepAlive(_handle);
            return this;
        }

        /// <summary>
        /// Set the keys used to decrypt the columns.
        /// </summary>
        /// <param name="columnDecryptionProperties">An array of <see cref="ColumnDecryptionProperties"/> objects.</param>
        /// <returns>This builder instance.</returns>
        public FileDecryptionPropertiesBuilder ColumnKeys(ColumnDecryptionProperties[] columnDecryptionProperties)
        {
            var handles = columnDecryptionProperties.Select(p => p.Handle.IntPtr).ToArray();
            ExceptionInfo.Check(FileDecryptionPropertiesBuilder_Column_Keys(_handle.IntPtr, handles, handles.Length));
            GC.KeepAlive(_handle);
            GC.KeepAlive(columnDecryptionProperties);
            return this;
        }

        /// <summary>
        /// Set the prefix verifier used to verify the additional authenticated data (AAD) prefix.
        /// </summary>
        /// <param name="aadPrefixVerifier">An <see cref="ParquetSharp.AadPrefixVerifier"/> object that provides a callback to verify the AAD prefix.</param>
        /// <returns>This builder instance.</returns>
        public FileDecryptionPropertiesBuilder AadPrefixVerifier(AadPrefixVerifier aadPrefixVerifier)
        {
            var gcHandle = aadPrefixVerifier?.CreateGcHandle() ?? IntPtr.Zero;

            try
            {
                ExceptionInfo.Check(FileDecryptionPropertiesBuilder_Aad_Prefix_Verifier(
                    _handle.IntPtr,
                    gcHandle,
                    ParquetSharp.AadPrefixVerifier.FreeGcHandleCallback,
                    ParquetSharp.AadPrefixVerifier.VerifyFuncCallback));
            }

            catch
            {
                ParquetSharp.AadPrefixVerifier.FreeGcHandleCallback(gcHandle);
                throw;
            }

            GC.KeepAlive(_handle);
            GC.KeepAlive(aadPrefixVerifier);

            return this;
        }

        /// <summary>
        /// Set the key retriever used to retrieve the decryption keys.
        /// </summary>
        /// <param name="keyRetriever">A <see cref="DecryptionKeyRetriever"/> object that provides a callback to retrieve the decryption keys.</param>
        /// <returns>This builder instance.</returns>
        public FileDecryptionPropertiesBuilder KeyRetriever(DecryptionKeyRetriever keyRetriever)
        {
            var gcHandle = keyRetriever?.CreateGcHandle() ?? IntPtr.Zero;

            try
            {
                ExceptionInfo.Check(FileDecryptionPropertiesBuilder_Key_Retriever(
                    _handle.IntPtr,
                    gcHandle,
                    DecryptionKeyRetriever.FreeGcHandleCallback,
                    DecryptionKeyRetriever.GetKeyFuncCallback));
            }

            catch
            {
                DecryptionKeyRetriever.FreeGcHandleCallback(gcHandle);
                throw;
            }

            GC.KeepAlive(_handle);
            GC.KeepAlive(keyRetriever);

            return this;
        }

        /// <summary>
        /// Disable footer signature verification.
        /// </summary>
        /// <returns>This builder instance.</returns>
        public FileDecryptionPropertiesBuilder DisableFooterSignatureVerification()
        {
            ExceptionInfo.Check(FileDecryptionPropertiesBuilder_Disable_Footer_Signature_Verification(_handle.IntPtr));
            GC.KeepAlive(_handle);
            return this;
        }

        /// <summary>
        /// Set the additional authenticated data (AAD) prefix.
        /// </summary>
        /// <param name="aadPrefix">A string representing the AAD prefix.</param>
        /// <returns>This builder instance.</returns>
        public FileDecryptionPropertiesBuilder AadPrefix(string aadPrefix)
        {
            ExceptionInfo.Check(FileDecryptionPropertiesBuilder_Aad_Prefix(_handle.IntPtr, aadPrefix));
            GC.KeepAlive(_handle);
            return this;
        }

        /// <summary>
        /// Allow plaintext (unencrypted) files.
        /// </summary>
        /// <returns>This builder instance.</returns>
        public FileDecryptionPropertiesBuilder PlaintextFilesAllowed()
        {
            ExceptionInfo.Check(FileDecryptionPropertiesBuilder_Plaintext_Files_Allowed(_handle.IntPtr));
            GC.KeepAlive(_handle);
            return this;
        }

        /// <summary>
        /// Build the <see cref="FileDecryptionProperties"/> object.
        /// </summary>
        /// <returns>The configured <see cref="FileDecryptionProperties"/> object.</returns>
        public FileDecryptionProperties Build() => new FileDecryptionProperties(ExceptionInfo.Return<IntPtr>(_handle, FileDecryptionPropertiesBuilder_Build));

        [DllImport(ParquetDll.Name)]
        private static extern IntPtr FileDecryptionPropertiesBuilder_Create(out IntPtr builder);

        [DllImport(ParquetDll.Name)]
        private static extern void FileDecryptionPropertiesBuilder_Free(IntPtr builder);

        [DllImport(ParquetDll.Name)]
        private static extern IntPtr FileDecryptionPropertiesBuilder_Footer_Key(IntPtr builder, in AesKey footerKey);

        [DllImport(ParquetDll.Name)]
        private static extern IntPtr FileDecryptionPropertiesBuilder_Column_Keys(IntPtr builder, IntPtr[] columnDecryptionProperties, int numProperties);

        [DllImport(ParquetDll.Name)]
        private static extern IntPtr FileDecryptionPropertiesBuilder_Key_Retriever(
            IntPtr builder,
            IntPtr gcHandle,
            DecryptionKeyRetriever.FreeGcHandleFunc freeGcHandle,
            DecryptionKeyRetriever.GetKeyFunc getKey);

        [DllImport(ParquetDll.Name)]
        private static extern IntPtr FileDecryptionPropertiesBuilder_Disable_Footer_Signature_Verification(IntPtr builder);

        [DllImport(ParquetDll.Name)]
        private static extern IntPtr FileDecryptionPropertiesBuilder_Aad_Prefix(IntPtr builder, string aadPrefix);

        [DllImport(ParquetDll.Name)]
        private static extern IntPtr FileDecryptionPropertiesBuilder_Aad_Prefix_Verifier(
            IntPtr builder,
            IntPtr gcHandle,
            AadPrefixVerifier.FreeGcHandleFunc freeGcHandle,
            AadPrefixVerifier.VerifyFunc getKey);

        [DllImport(ParquetDll.Name)]
        private static extern IntPtr FileDecryptionPropertiesBuilder_Plaintext_Files_Allowed(IntPtr builder);

        [DllImport(ParquetDll.Name)]
        private static extern IntPtr FileDecryptionPropertiesBuilder_Build(IntPtr builder, out IntPtr properties);

        private readonly ParquetHandle _handle;
    }
}
