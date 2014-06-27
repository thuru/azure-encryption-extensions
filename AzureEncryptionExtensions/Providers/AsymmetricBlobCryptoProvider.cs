﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace AzureBlobEncryption.Providers
{
    public sealed class AsymmetricBlobCryptoProvider : IBlobCryptoProvider
    {
        private readonly int DefaultKeySize = 4096;

        public byte[] CspBlob { get; set; }
        public int AsymmetricKeySize;

        public AsymmetricBlobCryptoProvider()
        {
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(DefaultKeySize))
            {
                CspBlob = rsa.ExportCspBlob(true);
                AsymmetricKeySize = rsa.KeySize;
            }
        }

        public AsymmetricBlobCryptoProvider(X509Certificate2 certificate, bool loadPrivateKeyIfAvailable = true)
        {
            RSACryptoServiceProvider rsa;
            
            if (loadPrivateKeyIfAvailable && certificate.HasPrivateKey)
                rsa = (RSACryptoServiceProvider)certificate.PrivateKey;
            else
                rsa = (RSACryptoServiceProvider)certificate.PublicKey.Key;

            CspBlob = rsa.ExportCspBlob(true);
            AsymmetricKeySize = rsa.KeySize;

            rsa.Dispose();
        }

        public void WriteKeyFile(string path)
        {
            throw new NotImplementedException();
        }

        public string ToKeyFileString()
        {
            //var json = new JavaScriptSerializer().Serialize(obj);
            throw new NotImplementedException();
        }


        public System.IO.Stream EncryptedStream(System.IO.Stream streamToEncrypt)
        {
            using (AesCryptoServiceProvider aesAlg = new AesCryptoServiceProvider())
            {
                // Set new key but retain randomized IV created during provider instantiation.
                aesAlg.Key = GenerateRandomKey();
                byte[] encryptedKey = EncryptKey(CspBlob, aesAlg.Key);

                // Create an encryptor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor();

                MemoryStream keyStream = new MemoryStream(encryptedKey);
                MemoryStream ivStream = new MemoryStream(aesAlg.IV);
                CryptoStream cryptoStream = new CryptoStream(streamToEncrypt, encryptor, CryptoStreamMode.Read);

                return new ConcatenatedStream(keyStream, ivStream, cryptoStream);
            }
        }

        public System.IO.Stream DecryptedStream(System.IO.Stream streamToDecrypt)
        {
            using (AesCryptoServiceProvider aesAlg = new AesCryptoServiceProvider())
            {
                // Get the AES key from the stream
                // The length will be the size of the RSA key which was used to encrypt
                // the 256 bit AES key (assuming the RSA key is always larger than 256 bit).
                byte[] encryptedKey = new byte[AsymmetricKeySize / 8];
                byte[] decryptedKey;
                streamToDecrypt.Read(encryptedKey, 0, encryptedKey.Length);
                decryptedKey = DecryptKey(CspBlob, encryptedKey);

                // Attempt to read IV from Stream
                byte[] ivBytes = new byte[aesAlg.BlockSize / 8];
                streamToDecrypt.Read(ivBytes, 0, ivBytes.Length);

                // Set key and initialization vector
                aesAlg.Key = decryptedKey;
                aesAlg.IV = ivBytes;

                // Create a decryptor to perform the stream transform.
                ICryptoTransform decryptor = aesAlg.CreateDecryptor();

                CryptoStream cryptoStream = new CryptoStream(streamToDecrypt, decryptor, CryptoStreamMode.Read);

                return cryptoStream;
            }
        }

        private static byte[] GenerateRandomKey()
        {
            // AES 256 key
            byte[] key = new byte[256 / 8];

            // Cryptographically strong random bytes
            RNGCryptoServiceProvider rngProvider = new RNGCryptoServiceProvider();
            rngProvider.GetNonZeroBytes(key);

            return key;
        }

        private static byte[] EncryptKey(byte[] cspBlob, byte[] key)
        {
            byte[] encryptedKey;

            // Encrypt using RSA provider
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.ImportCspBlob(cspBlob);
                encryptedKey = rsa.Encrypt(key, false);
            }

            return encryptedKey;
        }

        private static byte[] DecryptKey(byte[] cspBlob, byte[] encryptedKey)
        {
            byte[] decryptedKey;

            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
            {
                rsa.ImportCspBlob(cspBlob);
                decryptedKey = rsa.Decrypt(encryptedKey, false);
            }

            return decryptedKey;
        }
    }
}