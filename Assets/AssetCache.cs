/*
* Copyright (c) OpenSim project, http://sim.opensecondlife.org/
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
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
using libsecondlife.Packets;
using OpenSim;
using OpenSim.GridServers;

namespace OpenSim.Assets
{
	/// <summary>
	/// Manages local cache of assets and their sending to viewers.
	/// </summary>
	public class AssetCache : IAssetReceiver
	{
		public Dictionary<libsecondlife.LLUUID, AssetInfo> Assets;
		public Dictionary<libsecondlife.LLUUID, TextureImage> Textures;
		
		public List<AssetRequest> AssetRequests = new List<AssetRequest>();  //assets ready to be sent to viewers
		public List<AssetRequest> TextureRequests = new List<AssetRequest>(); //textures ready to be sent
		
		public Dictionary<LLUUID, AssetRequest> RequestedAssets = new Dictionary<LLUUID, AssetRequest>(); //Assets requested from the asset server
		public Dictionary<LLUUID, AssetRequest> RequestedTextures = new Dictionary<LLUUID, AssetRequest>(); //Textures requested from the asset server
		
		private IAssetServer _assetServer;
		private Thread _assetCacheThread;
		
		/// <summary>
		/// 
		/// </summary>
		public AssetCache( IAssetServer assetServer)
		{
			_assetServer = assetServer;
			_assetServer.SetReceiver(this);
			this._assetCacheThread = new Thread( new ThreadStart(RunAssetManager));
			this._assetCacheThread.IsBackground = true;
			this._assetCacheThread.Start();
		}
		
		/// <summary>
		/// 
		/// </summary>
		private void RunAssetManager()
		{
			//should be running in its own thread
			this.ProcessAssetQueue();
			this.ProcessTextureQueue();
			Thread.Sleep(100);
		}
		
		/// <summary>
		/// 
		/// </summary>
		private void ProcessTextureQueue()
		{
			if(this.TextureRequests.Count == 0)
			{
				//no requests waiting
				return;
			}
			int num;
			
			if(this.TextureRequests.Count < 5)
			{
				//lower than 5 so do all of them
				num = this.TextureRequests.Count;
			}
			else
			{
				num=5;
			}
			AssetRequest req;
			for(int i = 0; i < num; i++)
			{
				req=(AssetRequest)this.TextureRequests[i];
				
				if(req.PacketCounter == 0)
				{
					//first time for this request so send imagedata packet
					if(req.NumPackets == 1)
					{
						//only one packet so send whole file
						ImageDataPacket im = new ImageDataPacket();
						im.ImageID.Packets = 1;
						im.ImageID.ID = req.ImageInfo.FullID;
						im.ImageID.Size = (uint)req.ImageInfo.Data.Length;
						im.ImageData.Data = req.ImageInfo.Data;
						im.ImageID.Codec = 2;
						req.RequestUser.OutPacket(im);
						req.PacketCounter++;
						//req.ImageInfo.l= time;
						//System.Console.WriteLine("sent texture: "+req.image_info.FullID);
					}
					else
					{
						//more than one packet so split file up
						ImageDataPacket im = new ImageDataPacket();
						im.ImageID.Packets = (ushort)req.NumPackets;
						im.ImageID.ID = req.ImageInfo.FullID;
						im.ImageID.Size = (uint)req.ImageInfo.Data.Length;
						im.ImageData.Data = new byte[600];
						Array.Copy(req.ImageInfo.Data, 0, im.ImageData.Data, 0, 600);
						im.ImageID.Codec = 2;
						req.RequestUser.OutPacket(im);
						req.PacketCounter++;
						//req.ImageInfo.last_used = time;
						//System.Console.WriteLine("sent first packet of texture:
					}
				}
				else
				{
					//send imagepacket
					//more than one packet so split file up
					ImagePacketPacket im = new ImagePacketPacket();
					im.ImageID.Packet = (ushort)req.PacketCounter;
					im.ImageID.ID = req.ImageInfo.FullID;
					int size = req.ImageInfo.Data.Length - 600 - 1000*(req.PacketCounter - 1);
					if(size > 1000) size = 1000;
					im.ImageData.Data = new byte[size];
					Array.Copy(req.ImageInfo.Data, 600 + 1000*(req.PacketCounter - 1), im.ImageData.Data, 0, size);
					req.RequestUser.OutPacket(im);
					req.PacketCounter++;
					//req.ImageInfo.last_used = time;
					//System.Console.WriteLine("sent a packet of texture: "+req.image_info.FullID);
				}
			}
			
			//remove requests that have been completed
			for(int i = 0; i < num; i++)
			{
				req=(AssetRequest)this.TextureRequests[i];
				if(req.PacketCounter == req.NumPackets)
				{
					this.TextureRequests.Remove(req);
				}
			}
			
		}
		public void AssetReceived(AssetBase asset, bool IsTexture)
		{
			//check if it is a texture or not
			//then add to the correct cache list
			//then check for waiting requests for this asset/texture (in the Requested lists)
			//and move those requests into the Requests list.
			if(IsTexture)
			{
				TextureImage image = new TextureImage(asset);
				this.Textures.Add(image.FullID, image);
				if(this.RequestedTextures.ContainsKey(image.FullID))
				{
					AssetRequest req = this.RequestedTextures[image.FullID];
					req.ImageInfo = image;
					this.RequestedTextures.Remove(image.FullID);
					this.TextureRequests.Add(req);
				}
			}
			else
			{
				AssetInfo assetInf = new AssetInfo(asset);
				this.Assets.Add(assetInf.FullID, assetInf);
				if(this.RequestedAssets.ContainsKey(assetInf.FullID))
				{
					AssetRequest req = this.RequestedAssets[assetInf.FullID];
					req.AssetInf = assetInf;
					this.RequestedAssets.Remove(assetInf.FullID);
					this.AssetRequests.Add(req);
				}
			}
		}
		
		public void AssetNotFound(AssetBase asset)
		{
			//the asset server had no knowledge of requested asset
			
		}
		
		#region Assets
		/// <summary>
		/// 
		/// </summary>
		/// <param name="userInfo"></param>
		/// <param name="transferRequest"></param>
		public void AddAssetRequest(OpenSimClient userInfo, TransferRequestPacket transferRequest)
		{
			LLUUID requestID = new LLUUID(transferRequest.TransferInfo.Params, 0);
			//check to see if asset is in local cache, if not we need to request it from asset server.
			if(!this.Assets.ContainsKey(requestID))
			{
				//not found asset	
				// so request from asset server
				AssetRequest request = new AssetRequest();
				request.RequestUser = userInfo;
				request.RequestAssetID = requestID;
				request.TransferRequestID = transferRequest.TransferInfo.TransferID;
				this.RequestedAssets.Add(requestID,request);
				this._assetServer.RequestAsset(requestID, false);
				return;
			}
			//it is in our cache 
			AssetInfo asset = this.Assets[requestID];
			
			//work out how many packets it  should be sent in 
			// and add to the AssetRequests list
			AssetRequest req = new AssetRequest();
			req.RequestUser = userInfo;
			req.RequestAssetID = requestID;
			req.TransferRequestID = transferRequest.TransferInfo.TransferID;
			req.AssetInf = asset;
			
			if(asset.Data.LongLength>600) 
			{
				//over 600 bytes so split up file
				req.NumPackets = 1 + (int)(asset.Data.Length-600+999)/1000;
			}
			else
			{
				req.NumPackets = 1;
			}
			
			this.AssetRequests.Add(req);
		}
		
		/// <summary>
		/// 
		/// </summary>
		private void ProcessAssetQueue()
		{
			if(this.AssetRequests.Count == 0)
			{
				//no requests waiting
				return;
			}
			int num;
			
			if(this.AssetRequests.Count < 5)
			{
				//lower than 5 so do all of them
				num = this.AssetRequests.Count;
			}
			else
			{
				num=5;
			}
			AssetRequest req;
			for(int i = 0; i < num; i++)
			{
				req=(AssetRequest)this.AssetRequests[i];
				
				TransferInfoPacket Transfer = new TransferInfoPacket();
				Transfer.TransferInfo.ChannelType = 2;
				Transfer.TransferInfo.Status = 0;
				Transfer.TransferInfo.TargetType = 0;
				Transfer.TransferInfo.Params = req.RequestAssetID.GetBytes();
				Transfer.TransferInfo.Size = (int)req.AssetInf.Data.Length;
				Transfer.TransferInfo.TransferID = req.TransferRequestID;
				req.RequestUser.OutPacket(Transfer);
				
				if(req.NumPackets == 1)
				{
					TransferPacketPacket TransferPacket = new TransferPacketPacket();
					TransferPacket.TransferData.Packet = 0;
					TransferPacket.TransferData.ChannelType = 2;
					TransferPacket.TransferData.TransferID=req.TransferRequestID;
					TransferPacket.TransferData.Data = req.AssetInf.Data;
					TransferPacket.TransferData.Status = 1;
					req.RequestUser.OutPacket(TransferPacket);
				}
				else
				{
					//more than one packet so split file up , for now it can't be bigger than 2000 bytes
					TransferPacketPacket TransferPacket = new TransferPacketPacket();
					TransferPacket.TransferData.Packet = 0;
					TransferPacket.TransferData.ChannelType = 2;
					TransferPacket.TransferData.TransferID=req.TransferRequestID;
					byte[] chunk = new byte[1000];
					Array.Copy(req.AssetInf.Data,chunk,1000);
					TransferPacket.TransferData.Data = chunk;
					TransferPacket.TransferData.Status = 0;
					req.RequestUser.OutPacket(TransferPacket);	
					
					TransferPacket = new TransferPacketPacket();
					TransferPacket.TransferData.Packet = 1;
					TransferPacket.TransferData.ChannelType = 2;
					TransferPacket.TransferData.TransferID = req.TransferRequestID;
					byte[] chunk1 = new byte[(req.AssetInf.Data.Length-1000)];
					Array.Copy(req.AssetInf.Data, 1000, chunk1, 0, chunk1.Length);
					TransferPacket.TransferData.Data = chunk1;
					TransferPacket.TransferData.Status = 1;
					req.RequestUser.OutPacket(TransferPacket);
				}
				
			}
			
			//remove requests that have been completed
			for(int i = 0; i < num; i++)
			{
				this.AssetRequests.RemoveAt(i);
			}
			
		}
		
		#endregion
		
		#region Textures
		/// <summary>
		/// 
		/// </summary>
		/// <param name="userInfo"></param>
		/// <param name="imageID"></param>
		public void AddTextureRequest(OpenSimClient userInfo, LLUUID imageID)
		{
			//check to see if texture is in local cache, if not request from asset server
			if(!this.Textures.ContainsKey(imageID))
			{
				//not is cache so request from asset server
				AssetRequest request = new AssetRequest();
				request.RequestUser = userInfo;
				request.RequestAssetID = imageID;
				request.IsTextureRequest = true;
				this.RequestedTextures.Add(imageID, request);
				this._assetServer.RequestAsset(imageID, true);
				return;
			}
			TextureImage imag = this.Textures[imageID];
			AssetRequest req = new AssetRequest();
			req.RequestUser = userInfo;
			req.RequestAssetID = imageID;
			req.IsTextureRequest = true;
			req.ImageInfo = imag;
			
			if(imag.Data.LongLength>600) 
			{
				//over 600 bytes so split up file
				req.NumPackets = 1 + (int)(imag.Data.Length-600+999)/1000;
			}
			else
			{
				req.NumPackets = 1;
			}
			
			this.TextureRequests.Add(req);
		}
		#endregion
		
	}

	public class AssetRequest
	{
		public OpenSimClient RequestUser;
		public LLUUID RequestAssetID;
		public AssetInfo AssetInf;
		public TextureImage ImageInfo;
		public LLUUID TransferRequestID;
		public long DataPointer = 0;
		public int NumPackets = 0;
		public int PacketCounter = 0;
		public bool IsTextureRequest;
		//public bool AssetInCache;
		//public int TimeRequested; 
		
		public AssetRequest()
		{
			
		}
	}
	
	public class AssetBase
	{
		public byte[] Data;
		public LLUUID FullID;
		public sbyte Type;
		public sbyte InvType;
		public string Name;
		public string Description;
		
		public AssetBase()
		{
			
		}
	}
	
	public class AssetInfo : AssetBase
	{
		public AssetInfo()
		{
			
		}
		
		public AssetInfo(AssetBase aBase)
		{
			Data= aBase.Data;
			FullID = aBase.FullID;
			Type = aBase.Type;
			InvType = aBase.InvType;
			Name= aBase.Name;
			Description = aBase.Description;
		}
	}
	
	public class TextureImage : AssetBase
	{
		public TextureImage()
		{
			
		}
		
		public TextureImage(AssetBase aBase)
		{
			Data= aBase.Data;
			FullID = aBase.FullID;
			Type = aBase.Type;
			InvType = aBase.InvType;
			Name= aBase.Name;
			Description = aBase.Description;
		}
	}

}
