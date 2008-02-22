/*
* Copyright (c) Contributors, http://opensimulator.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System;
using System.Collections.Generic;
using System.Threading;
using libsecondlife;
using OpenSim.Framework.Console;
using OpenSim.Framework.AssetLoader.Filesystem;

namespace OpenSim.Framework.Communications.Cache
{
    public abstract class AssetServerBase : IAssetServer
    {
        protected IAssetReceiver m_receiver;
        protected BlockingQueue<AssetRequest> m_assetRequests;
        protected Thread m_localAssetServerThread;
        protected IAssetProvider m_assetProvider;
        protected object m_syncLock = new object();

        // Temporarily hardcoded - should be a plugin
        protected IAssetLoader assetLoader = new AssetLoaderFileSystem();
        
        protected abstract void StoreAsset(AssetBase asset);
        protected abstract void CommitAssets();

        public abstract LLUUID ExistsAsset(sbyte assetType, string name); // rex new function for "replace assets" functionality      

        /// <summary>
        /// This method must be implemented by a subclass to retrieve the asset named in the 
        /// AssetRequest.  If the asset is not found, null should be returned.
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        protected abstract AssetBase GetAsset(AssetRequest req);

        /// <summary>
        /// Process an asset request.  This method will call GetAsset(AssetRequest req) 
        /// on the subclass.
        /// </summary>
        /// <param name="req"></param>
        protected virtual void ProcessRequest(AssetRequest req)
        {
            AssetBase asset = GetAsset(req);

            if (asset != null)
            {
                MainLog.Instance.Verbose(
                    "ASSET", "Asset {0} received from asset server", req.AssetID);

                m_receiver.AssetReceived(asset, req.IsTexture);
            }
            else
            {
                MainLog.Instance.Error(
                    "ASSET", "Asset {0} not found by asset server", req.AssetID);

                m_receiver.AssetNotFound(req.AssetID);
            }
        }

        public virtual void LoadDefaultAssets()
        {
            MainLog.Instance.Verbose("ASSETSERVER", "Setting up asset database");

            assetLoader.ForEachDefaultXmlAsset(StoreAsset);

            CommitAssets();
        }


        public AssetServerBase()
        {
            MainLog.Instance.Verbose("ASSETSERVER", "Starting asset storage system");
            m_assetRequests = new BlockingQueue<AssetRequest>();

            m_localAssetServerThread = new Thread(RunRequests);
            m_localAssetServerThread.IsBackground = true;
            m_localAssetServerThread.Start();
        }

        private void RunRequests()
        {
            while (true) // Since it's a 'blocking queue'
            {
                try
                {
                    AssetRequest req = m_assetRequests.Dequeue();

                    ProcessRequest(req);
                }
                catch (Exception e)
                {
                    MainLog.Instance.Error("ASSETSERVER", e.Message);
                }
            }
        }

        public void SetReceiver(IAssetReceiver receiver)
        {
            m_receiver = receiver;
        }

        public void RequestAsset(LLUUID assetID, bool isTexture)
        {
            AssetRequest req = new AssetRequest();
            req.AssetID = assetID;
            req.IsTexture = isTexture;
            m_assetRequests.Enqueue(req);

            MainLog.Instance.Verbose("ASSET", "Added {0} to request queue", assetID);
        }

        public virtual void UpdateAsset(AssetBase asset)
        {
            lock (m_syncLock)
            {
                m_assetProvider.UpdateAsset(asset);
                m_assetProvider.CommitAssets();
            }
        }

        public void StoreAndCommitAsset(AssetBase asset)
        {
            lock (m_syncLock)
            {
                StoreAsset(asset);
                CommitAssets();
            }
        }

        // rex, new function
        public List<AssetBase> GetAssetList(int vAssetType)
        {
            lock (m_syncLock)
            {
                return m_assetProvider.GetAssetList(vAssetType);
            }
        }

        // rex, new function
        public virtual AssetBase FetchAsset(LLUUID assetID)
        {
            lock (m_syncLock)
            {
                return m_assetProvider.FetchAsset(assetID);
            }
        }

        // rex, new function
        public virtual bool ExistsAsset(LLUUID assetID)
        {
            lock (m_syncLock)
            {
                return m_assetProvider.ExistsAsset(assetID);
            }
        }

        public virtual void Close()
        {
            m_localAssetServerThread.Abort();
        }

        public void SetServerInfo(string ServerUrl, string ServerKey)
        {
        }
    }
}