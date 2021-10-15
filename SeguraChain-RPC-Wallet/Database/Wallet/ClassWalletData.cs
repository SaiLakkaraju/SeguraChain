using System.Numerics;

namespace SeguraChain_RPC_Wallet.Database.Wallet
{
    public class ClassWalletData
    {
        /// <summary>
        /// General informations.
        /// </summary>
        public string WalletAddress;
        public string WalletPublicKey;
        public string WalletPrivateKey;

        /// <summary>
        /// Wallet balances.
        /// </summary>
        public BigInteger WalletBalance;
        public BigInteger WalletPendingBalance;
        public long WalletBlockHeight;
    }
}
