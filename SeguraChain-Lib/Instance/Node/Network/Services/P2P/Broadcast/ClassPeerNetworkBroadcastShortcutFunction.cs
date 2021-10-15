﻿using SeguraChain_Lib.Algorithm;
using SeguraChain_Lib.Blockchain.Wallet.Function;
using SeguraChain_Lib.Instance.Node.Network.Database;
using SeguraChain_Lib.Instance.Node.Network.Database.Manager;
using SeguraChain_Lib.Instance.Node.Network.Enum.P2P.Packet;
using SeguraChain_Lib.Instance.Node.Network.Services.P2P.Sync.ClientSync.ClientConnect.Object;
using SeguraChain_Lib.Instance.Node.Network.Services.P2P.Sync.Packet;
using SeguraChain_Lib.Instance.Node.Setting.Object;
using SeguraChain_Lib.Utility;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SeguraChain_Lib.Instance.Node.Network.Services.P2P.Broadcast
{
    public class ClassPeerNetworkBroadcastShortcutFunction
    {
        /// <summary>
        /// Build and send a packet, await the expected packet response to receive.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="R"></typeparam>
        /// <param name="peerNetworkClientSyncObject"></param>
        /// <param name="packetType"></param>
        /// <param name="packetToSend"></param>
        /// <param name="peerIpTarget"></param>
        /// <param name="peerUniqueIdTarget"></param>
        /// <param name="peerNetworkSetting"></param>
        /// <param name="packetTypeExpected"></param>
        /// <param name="cancellation"></param>
        /// <returns></returns>
        public static async Task<R> SendBroadcastPacket<T, R>(ClassPeerNetworkClientSyncObject peerNetworkClientSyncObject, ClassPeerEnumPacketSend packetType, T packetToSend, string peerIpTarget, string peerUniqueIdTarget, ClassPeerNetworkSettingObject peerNetworkSetting, ClassPeerEnumPacketResponse packetTypeExpected, CancellationTokenSource cancellation)
        {
            
            ClassPeerPacketSendObject packetSendObject = new ClassPeerPacketSendObject(peerNetworkSetting.PeerUniqueId,
            ClassPeerDatabase.DictionaryPeerDataObject[peerIpTarget][peerUniqueIdTarget].PeerInternPublicKey,
            ClassPeerDatabase.DictionaryPeerDataObject[peerIpTarget][peerUniqueIdTarget].PeerClientLastTimestampPeerPacketSignatureWhitelist)
            {
                PacketOrder = packetType,
                PacketContent = ClassUtility.SerializeData(packetToSend)
            };

            packetSendObject = BuildSignedPeerSendPacketObject(packetSendObject, peerIpTarget, peerUniqueIdTarget, true, peerNetworkSetting, cancellation);

            if (packetSendObject == null)
                return default(R);

            if (!await peerNetworkClientSyncObject.TrySendPacketToPeerTarget(packetSendObject.GetPacketData(), cancellation, packetTypeExpected, false, false))
                return default(R);

            if (peerNetworkClientSyncObject.PeerPacketReceivedIgnored)
                return default(R);

            if (peerNetworkClientSyncObject.PeerPacketReceived == null)
                return default(R);

            if (peerNetworkClientSyncObject.PeerPacketReceived.PacketOrder != packetTypeExpected)
                return default(R);

            bool peerPacketSignatureValid = ClassPeerCheckManager.CheckPeerClientWhitelistStatus(peerIpTarget, peerUniqueIdTarget, peerNetworkSetting) ? true : ClassWalletUtility.WalletCheckSignature(peerNetworkClientSyncObject.PeerPacketReceived.PacketHash, peerNetworkClientSyncObject.PeerPacketReceived.PacketSignature, ClassPeerDatabase.DictionaryPeerDataObject[peerIpTarget][peerUniqueIdTarget].PeerClientPublicKey);

            if (!peerPacketSignatureValid)
                return default(R);


            Tuple<byte[], bool> packetTupleDecrypted = ClassPeerDatabase.DictionaryPeerDataObject[peerIpTarget][peerUniqueIdTarget].GetInternCryptoStreamObject.DecryptDataProcess(Convert.FromBase64String(peerNetworkClientSyncObject.PeerPacketReceived.PacketContent));
            if (packetTupleDecrypted.Item1 == null || !packetTupleDecrypted.Item2)
            {
                if (ClassAes.DecryptionProcess(Convert.FromBase64String(peerNetworkClientSyncObject.PeerPacketReceived.PacketContent), ClassPeerDatabase.DictionaryPeerDataObject[peerIpTarget][peerUniqueIdTarget].PeerInternPacketEncryptionKey, ClassPeerDatabase.DictionaryPeerDataObject[peerIpTarget][peerUniqueIdTarget].PeerInternPacketEncryptionKeyIv, out byte[] packetDecrypted))
                    packetTupleDecrypted = new Tuple<byte[], bool>(packetDecrypted, true);
            }

            if (packetTupleDecrypted.Item1 == null || !packetTupleDecrypted.Item2)
                return default(R);

            if (!ClassUtility.TryDeserialize(packetTupleDecrypted.Item1.GetStringFromByteArrayAscii(), out R peerPacketReceived))
                return default(R);

            if (EqualityComparer<R>.Default.Equals(peerPacketReceived, default(R)))
                return default(R);


            return peerPacketReceived;
        }

        #region Static peer packet signing/encryption function.

        /// <summary>
        /// Build the packet content encrypted with peer auth keys and the internal private key assigned to the peer target for sign the packet.
        /// </summary>
        /// <param name="sendObject"></param>
        /// <param name="peerIp"></param>
        /// <param name="peerUniqueId"></param>
        /// <param name="cancellation"></param>
        /// <returns></returns>
        public static ClassPeerPacketSendObject BuildSignedPeerSendPacketObject(ClassPeerPacketSendObject sendObject, string peerIp, string peerUniqueId, bool forceSignature, ClassPeerNetworkSettingObject peerNetworkSettingObject, CancellationTokenSource cancellation)
        {
            if (ClassPeerDatabase.ContainsPeer(peerIp, peerUniqueId))
            {
                byte[] packetContentEncrypted;

                if (ClassPeerDatabase.DictionaryPeerDataObject[peerIp][peerUniqueId].GetInternCryptoStreamObject != null)
                {
                    if (cancellation == null)
                    {
                        if (!ClassAes.EncryptionProcess(ClassUtility.GetByteArrayFromStringUtf8(sendObject.PacketContent), ClassPeerDatabase.DictionaryPeerDataObject[peerIp][peerUniqueId].PeerInternPacketEncryptionKey, ClassPeerDatabase.DictionaryPeerDataObject[peerIp][peerUniqueId].PeerInternPacketEncryptionKeyIv, out packetContentEncrypted))
                            return null;
                    }
                    else
                    {
                        packetContentEncrypted = ClassPeerDatabase.DictionaryPeerDataObject[peerIp][peerUniqueId].GetInternCryptoStreamObject.EncryptDataProcess(ClassUtility.GetByteArrayFromStringUtf8(sendObject.PacketContent));

                        if (packetContentEncrypted == null)
                        {
                            if (!ClassAes.EncryptionProcess(ClassUtility.GetByteArrayFromStringUtf8(sendObject.PacketContent), ClassPeerDatabase.DictionaryPeerDataObject[peerIp][peerUniqueId].PeerInternPacketEncryptionKey, ClassPeerDatabase.DictionaryPeerDataObject[peerIp][peerUniqueId].PeerInternPacketEncryptionKeyIv, out packetContentEncrypted))
                                return null;
                        }
                    }
                }
                else
                {
                    if (!ClassAes.EncryptionProcess(ClassUtility.GetByteArrayFromStringUtf8(sendObject.PacketContent), ClassPeerDatabase.DictionaryPeerDataObject[peerIp][peerUniqueId].PeerInternPacketEncryptionKey, ClassPeerDatabase.DictionaryPeerDataObject[peerIp][peerUniqueId].PeerInternPacketEncryptionKeyIv, out packetContentEncrypted))
                        return null;
                }


                sendObject.PacketContent = Convert.ToBase64String(packetContentEncrypted);
                sendObject.PacketHash = ClassUtility.GenerateSha3512FromString(sendObject.PacketContent + sendObject.PacketOrder);

                if (ClassPeerDatabase.DictionaryPeerDataObject[peerIp].ContainsKey(peerUniqueId))
                {
                    if (ClassPeerCheckManager.CheckPeerClientWhitelistStatus(peerIp, peerUniqueId, peerNetworkSettingObject) || forceSignature)
                    {
                        if (ClassPeerDatabase.DictionaryPeerDataObject[peerIp][peerUniqueId].GetClientCryptoStreamObject != null && cancellation != null)
                            sendObject.PacketSignature = ClassPeerDatabase.DictionaryPeerDataObject[peerIp][peerUniqueId].GetClientCryptoStreamObject.DoSignatureProcess(sendObject.PacketHash, ClassPeerDatabase.DictionaryPeerDataObject[peerIp][peerUniqueId].PeerInternPrivateKey);
                        else
                            sendObject.PacketSignature = ClassWalletUtility.WalletGenerateSignature(ClassPeerDatabase.DictionaryPeerDataObject[peerIp][peerUniqueId].PeerInternPrivateKey, sendObject.PacketHash);
                    }
                }
            }
            return sendObject;
        }

        #endregion

        #region Check Peer numeric keys signatures on packets

        /// <summary>
        /// Check the peer seed numeric packet signature, compare with the list of peer listed by sovereign updates.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="peerIp"></param>
        /// <param name="peerUniqueId"></param>
        /// <param name="objectData"></param>
        /// <param name="packetNumericHash"></param>
        /// <param name="packetNumericSignature"></param>
        /// <param name="peerNetworkSettingObject"></param>
        /// <param name="cancellation"></param>
        /// <param name="numericPublicKeyOut"></param>
        /// <returns></returns>
        public static bool CheckPeerSeedNumericPacketSignature<T>(string peerIp, string peerUniqueId, T objectData, string packetNumericHash, string packetNumericSignature, ClassPeerNetworkSettingObject peerNetworkSettingObject, CancellationTokenSource cancellation, out string numericPublicKeyOut)
        {
            // Default value.
            numericPublicKeyOut = string.Empty;

            if (!peerNetworkSettingObject.PeerEnableSovereignPeerVote)
                return false;

            if (packetNumericHash.IsNullOrEmpty(out _) || packetNumericSignature.IsNullOrEmpty(out _))
                return false;

            if (!ClassPeerCheckManager.PeerHasSeedRank(peerIp, peerUniqueId, out numericPublicKeyOut, out _))
                return false;

            return ClassPeerCheckManager.CheckPeerSeedNumericPacketSignature(ClassUtility.SerializeData(objectData), packetNumericHash, packetNumericSignature, numericPublicKeyOut, cancellation);
        }

        #endregion
    }
}
