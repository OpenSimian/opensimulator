using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using OpenSim.Framework.User;
using OpenSim.Framework.Grid;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Interfaces;
using libsecondlife;

namespace OpenSim.Framework.LocalServers
{
    class LocalUserProfileManager : UserProfileManager 
    {
        private IGridServer _gridServer;

        public LocalUserProfileManager(IGridServer gridServer)
		{
			_gridServer = gridServer;
		}

        public override void CustomiseResponse(ref System.Collections.Hashtable response, UserProfile theUser)
        {
            uint circode = (uint)response["circuit_code"];
            theUser.AddSimCircuit(circode, LLUUID.Random());
            response["home"] = "{'region_handle':[r" + (997 * 256).ToString() + ",r" + (996 * 256).ToString() + "], 'position':[r" + theUser.homepos.X.ToString() + ",r" + theUser.homepos.Y.ToString() + ",r" + theUser.homepos.Z.ToString() + "], 'look_at':[r" + theUser.homelookat.X.ToString() + ",r" + theUser.homelookat.Y.ToString() + ",r" + theUser.homelookat.Z.ToString() + "]}";
            response["sim_port"] = OpenSim_Main.cfg.IPListenPort;
            response["sim_ip"] = OpenSim_Main.cfg.IPListenAddr;
            response["region_y"] = (Int32)996 * 256;
            response["region_x"] = (Int32)997* 256;

            string first;
            string last;
            if (response.Contains("first"))
            {
                first = (string)response["first"];
            }
            else
            {
                first = "test";
            }

            if (response.Contains("last"))
            {
                last = (string)response["last"];
            }
            else
            {
                last = "User";
            }

            ArrayList InventoryList = (ArrayList)response["inventory-skeleton"];
            Hashtable Inventory1 = (Hashtable)InventoryList[0];

            Login _login = new Login();
            //copy data to login object
            _login.First = first;
            _login.Last = last;
            _login.Agent = new LLUUID((string)response["agent_id"]) ;
            _login.Session = new LLUUID((string)response["session_id"]);
            _login.BaseFolder = null;
            _login.InventoryFolder = new LLUUID((string)Inventory1["folder_id"]);

            //working on local computer if so lets add to the gridserver's list of sessions?
            if (OpenSim_Main.gridServers.GridServer.GetName() == "Local")
            {
                ((LocalGridBase)this._gridServer).AddNewSession(_login);
            }
        }
    }
}
