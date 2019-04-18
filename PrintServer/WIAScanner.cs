using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;

namespace PrintServer
{
    class WIAScanner
    {
        const string wiaFormatJPEG = "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}";

        public static List<Image> Scan(string scannerId)
        {
            List<Image> images = new List<Image>();
            // select the correct scanner using the provided scannerId parameter
            WIA.DeviceManager manager = new WIA.DeviceManager();
            WIA.Device device = null;
            foreach (WIA.DeviceInfo info in manager.DeviceInfos)
            {
                if (info.DeviceID == scannerId)
                {
                    // connect to scanner
                    device = info.Connect();
                    break;
                }
            }
            WIA.Item item = device.Items[1] as WIA.Item;
            try
            {
                // scan image
                WIA.ICommonDialog wiaCommonDialog = new WIA.CommonDialog();
                WIA.ImageFile image = wiaCommonDialog.ShowTransfer(item, wiaFormatJPEG, false);
                // save to temp file
                string fileName = Path.GetTempFileName();
                File.Delete(fileName);
                image.SaveFile(fileName);
                image = null;
                // add file to output list
                images.Add(Image.FromFile(fileName));
            }
            catch (Exception exc)
            {
                throw exc;
            }
            finally
            {
                item = null;
            }
            return images;
        }
    }
}
