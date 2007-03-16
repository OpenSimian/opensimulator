using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Utilities;

namespace OpenSim.Framework.User
{
    public class UserProfileManagerBase
    {

        public Dictionary<LLUUID, UserProfile> UserProfiles = new Dictionary<LLUUID, UserProfile>();

        public UserProfileManagerBase()
        {
        }

        public virtual void InitUserProfiles()
        {
            // TODO: need to load from database
        }

        public UserProfile GetProfileByName(string firstname, string lastname)
        {
            foreach (libsecondlife.LLUUID UUID in UserProfiles.Keys)
            {
                if ((UserProfiles[UUID].firstname == firstname) && (UserProfiles[UUID].lastname == lastname))
                {
                    return UserProfiles[UUID];
                }
            }
            return null;
        }

        public UserProfile GetProfileByLLUUID(LLUUID ProfileLLUUID)
        {
            return UserProfiles[ProfileLLUUID];
        }

        public virtual bool AuthenticateUser(string firstname, string lastname, string passwd)
        {
            UserProfile TheUser = GetProfileByName(firstname, lastname);
            if (TheUser != null)
            {
                if (TheUser.MD5passwd == passwd)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

        }

        public void SetGod(LLUUID GodID)
        {
            this.UserProfiles[GodID].IsGridGod = true;
        }

        public virtual UserProfile CreateNewProfile(string firstname, string lastname, string MD5passwd)
        {
            UserProfile newprofile = new UserProfile();
            newprofile.homeregionhandle = Helpers.UIntsToLong((997 * 256), (996 * 256));
            newprofile.firstname = firstname;
            newprofile.lastname = lastname;
            newprofile.MD5passwd = MD5passwd;
            newprofile.UUID = LLUUID.Random();
            this.UserProfiles.Add(newprofile.UUID, newprofile);
            return newprofile;
        }

    }
}
