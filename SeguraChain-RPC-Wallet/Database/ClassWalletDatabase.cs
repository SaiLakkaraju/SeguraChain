using LZ4;
using SeguraChain_Lib.Algorithm;
using SeguraChain_Lib.Utility;
using SeguraChain_RPC_Wallet.Database.Wallet;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;

namespace SeguraChain_RPC_Wallet.Database
{
    public class ClassWalletDatabase
    {
        private ConcurrentDictionary<string, ClassWalletData> _dictionaryWallet;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ClassWalletDatabase()
        {
            _dictionaryWallet = new ConcurrentDictionary<string, ClassWalletData>();
        }

        /// <summary>
        /// Load wallet database.
        /// </summary>
        /// <param name="walletDatabasePath"></param>
        /// <param name="walletDatabasePassword"></param>
        /// <returns></returns>
        public bool LoadWalletDatabase(string walletDatabasePath, string walletDatabasePassword)
        {
            if (!ClassAes.GenerateKey(ClassUtility.GetByteArrayFromStringUtf8(walletDatabasePassword), true, out byte[] walletDatabaseEncryptionKey))
                return false;

            byte[] walletDatabaseEncryptionIv = ClassAes.GenerateIv(walletDatabaseEncryptionKey);

            using (FileStream fileStream = new FileStream(walletDatabasePath, FileMode.Open))
            {
                using (StreamReader reader = new StreamReader(new LZ4Stream(fileStream, LZ4StreamMode.Decompress, LZ4StreamFlags.HighCompression)))
                {
                    string line;
                    int lineIndex = 0;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!ClassAes.DecryptionProcess(Convert.FromBase64String(line), walletDatabaseEncryptionKey, walletDatabaseEncryptionIv, out byte[] walletDataBytes))
                        {
#if DEBUG
                            Debug.WriteLine("Can't deserialize wallet line data " + walletDataBytes.GetStringFromByteArrayUtf8() + " at line index " + lineIndex);
#endif
                            continue;
                        }

                        if (!ClassUtility.TryDeserialize(walletDataBytes.GetStringFromByteArrayUtf8(), out ClassWalletData walletData))
                        {
#if DEBUG
                            Debug.WriteLine("Can't decrypt wallet line data at line index " + lineIndex);
#endif
                            continue;
                        }

                        if (_dictionaryWallet.ContainsKey(walletData.WalletAddress))
                        {
#if DEBUG
                            Debug.WriteLine(walletData.WalletAddress + " already inserted.");
#endif
                            continue;
                        }

                        if (!_dictionaryWallet.TryAdd(walletData.WalletAddress, walletData))
                        {
#if DEBUG
                            Debug.WriteLine("Can't insert " + walletData.WalletAddress + " into the database.");
#endif
                        }

                        lineIndex++;
                    }

                }
            }

            return true;
        }

        /// <summary>
        /// Save wallet database.
        /// </summary>
        /// <param name="walletDatabasePath"></param>
        /// <param name="walletDatabasePassword"></param>
        /// <returns></returns>
        public bool SaveWalletDatabase(string walletDatabasePath, string walletDatabasePassword)
        {
            if (!ClassAes.GenerateKey(ClassUtility.GetByteArrayFromStringUtf8(walletDatabasePassword), true, out byte[] walletDatabaseEncryptionKey))
                return false;

            byte[] walletDatabaseEncryptionIv = ClassAes.GenerateIv(walletDatabaseEncryptionKey);

            using (FileStream fileStream = new FileStream(walletDatabasePath, FileMode.Open))
            {
                using (StreamWriter writer = new StreamWriter(new LZ4Stream(fileStream, LZ4StreamMode.Decompress, LZ4StreamFlags.HighCompression)))
                {
                    foreach (ClassWalletData walletData in _dictionaryWallet.Values)
                    {
                        if (!ClassAes.EncryptionProcess(ClassUtility.GetByteArrayFromStringUtf8(ClassUtility.SerializeData(walletData)), walletDatabaseEncryptionKey, walletDatabaseEncryptionIv, out byte[] walletDataEncrypted))
                        {
#if DEBUG
                            Debug.WriteLine("Can't encrypt wallet data " + walletData.WalletAddress);
#endif
                            continue;
                        }

                        writer.WriteLine(Convert.ToBase64String(walletDataEncrypted));
                    }
                }
            }

            return true;
        }
    }
}
