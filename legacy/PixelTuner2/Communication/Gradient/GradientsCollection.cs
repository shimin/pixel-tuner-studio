using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Communication.Gradient
{
    public class GradientsCollection
    {
        #region Fields

        Dictionary<string, GradientsCollectionItem> _descriptors = null;
        Guid emptyFileName = Guid.NewGuid();

        #endregion

        #region Constructors

        public GradientsCollection(List<GradientsCollectionItem> descriptors)
        {
            _descriptors = new Dictionary<string, GradientsCollectionItem>();
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

        public GradientsCollectionItem GetItem(string path)
        {
            string filename = String.IsNullOrEmpty(path) ? emptyFileName.ToString() : path;
            
            if (_descriptors.ContainsKey(filename))
                return _descriptors[filename];

            return null;
        }

        #endregion
    }
}
