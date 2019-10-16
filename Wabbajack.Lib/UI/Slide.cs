using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Wabbajack.Lib
{
    public class Slide
    {
        public Slide(string modName, string modID, string modDescription, string modAuthor, bool isNSFW, string modUrl, string imageURL)
        {
            ModName = modName;
            ModDescription = modDescription;
            ModAuthor = modAuthor;
            IsNSFW = isNSFW;
            ModURL = modUrl;
            ModID = modID;
            ImageURL = imageURL;
        }

        public string ModName { get; }
        public string ModDescription { get; }
        public string ModAuthor { get; }
        public bool IsNSFW { get; }
        public string ModURL { get; }
        public string ModID { get; }
        public BitmapImage Image { get; set; }
        public string ImageURL { get; }

    }
}
