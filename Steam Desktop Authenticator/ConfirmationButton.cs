using SteamAuth;
using System.Windows.Forms;

namespace Steam_Desktop_Authenticator
{
    public class ConfirmationButton : Button
    {
        public Confirmation Confirmation { get; set; }
        public Panel ParentPanel { get; set; }
    }

}
