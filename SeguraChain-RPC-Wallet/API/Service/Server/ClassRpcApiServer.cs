using SeguraChain_RPC_Wallet.Config;
using System.Threading;
using System.Threading.Tasks;

namespace SeguraChain_RPC_Wallet.API.Service.Server
{
    public class ClassRpcApiServer
    {
        private CancellationTokenSource _cancellationApiServer;
        private ClassRpcConfig _rpcConfig;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="rpcConfig"></param>
        public ClassRpcApiServer(ClassRpcConfig rpcConfig)
        {
            _rpcConfig = rpcConfig;
        }


        /// <summary>
        /// Launch the RPC API Server.
        /// </summary>
        public void StartApiServer()
        {
            _cancellationApiServer = new CancellationTokenSource();

            try
            {
 
            }
            catch
            {
                // Ignored, catch the exception once the task is cancelled.
            }
        }

        /// <summary>
        /// Stop the RPC API Server.
        /// </summary>
        public void StopApiServer()
        {

        }
    }
}
