using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace Communication.Pixelate
{
    public class ImagesCollection
    {
        #region Fields

        Dictionary<string, ImagesCollectionItem> _descriptors = null;
        Guid emptyFileName = Guid.NewGuid();

        #endregion

        #region Constructors

        //public ImagesCollection(List<string> imageFilePaths, byte targetHeight, ColorDepth depth)
        //{
        //    _descriptors = new Dictionary<string, ImagesCollectionItem>();
        //    foreach (var path in imageFilePaths)
        //    {
        //        string filename = String.IsNullOrEmpty(path) ? emptyFileName.ToString() : path;
        //        // don't add/rewrite already existing images
        //        if (!_descriptors.ContainsKey(filename))
        //        {
        //            _descriptors.Add(filename, new ImagesCollectionItem(filename, targetHeight, depth));
        //        }
        //    }
        //}

        public ImagesCollection(List<ImagesCollectionItem> descriptors)
        {
            _descriptors = new Dictionary<string, ImagesCollectionItem>();
            foreach (var descriptor in descriptors)
            {
                // don't add/rewrite already existing images
                string filename = String.IsNullOrEmpty(descriptor.Path) ? emptyFileName.ToString() : descriptor.Key;
                if (!_descriptors.ContainsKey(filename))
                {
                    _descriptors.Add(filename, descriptor);
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Sets images block start address
        /// </summary>
        /// <param name="adress"></param>
        public void SetBlockAddress(UInt32 adress)
        {
            UInt32 adr = adress;
            UInt32 adrLen = adress;
            foreach (var kvp in _descriptors)
            {
                kvp.Value.StartAddress = adr;
                adr += kvp.Value.BinaryLength;
                //Console.WriteLine(string.Format("Dec: adr={0}, adrLen={1}, end-start={2}, len={3} ||| Hex: adr={4}, adrLen={5}, end-start={6}, len={7}", 
                //    adr.ToString("D8"), 
                //    adrLen.ToString("D8"), 
                //    (kvp.Value.EndAddress - kvp.Value.StartAddress).ToString("D8"),
                //    (kvp.Value.Length).ToString("D8"),
                //    adr.ToString("X4"), 
                //    adrLen.ToString("X4"), 
                //    (kvp.Value.EndAddress - kvp.Value.StartAddress).ToString("X4"),
                //    (kvp.Value.Length).ToString("X4")
                //));
            }
        }

        internal int GetSize(byte targetHeight)
        {
            int size = 0;
            foreach (var desc in _descriptors.Values)
            {
                size += desc.GetSize(targetHeight);
            }
            return size;
        }

        public List<byte> GetBinary(byte targetHeight)
        {
            List<byte> result = new List<byte>();
            foreach (var desc in _descriptors.Values)
            {
                result.AddRange(desc.GetBinary(targetHeight));
            }
            return result;
        }

        public ImagesCollectionItem GetItem(string path)
        {
            string filename = String.IsNullOrEmpty(path) ? emptyFileName.ToString() : path;
            
            if (_descriptors.ContainsKey(filename))
                return _descriptors[filename];

            return null;
        }

        #endregion
    }
}
