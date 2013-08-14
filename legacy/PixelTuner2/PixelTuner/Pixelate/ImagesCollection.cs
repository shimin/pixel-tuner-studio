using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using PixelTuner.AuxClasses;

namespace PixelTuner.Pixelate
{
    public class ImagesCollection
    {
        #region Fields

        Dictionary<string, ImagesCollectionItem> _descriptors = null;

        #endregion

        #region Constructors

        public ImagesCollection(List<string> imageFilePaths, byte targetHeight)
        {
            _descriptors = new Dictionary<string, ImagesCollectionItem>();
            foreach (var path in imageFilePaths)
            {
                // don't add/rewrite already existing images
                if (!_descriptors.ContainsKey(path))
                {
                    _descriptors.Add(path, new ImagesCollectionItem(path, targetHeight));
                }
            }
        }

        public ImagesCollection(List<ImagesCollectionItem> descriptors)
        {
            _descriptors = new Dictionary<string, ImagesCollectionItem>();
            foreach (var descriptor in descriptors)
            {
                // don't add/rewrite already existing images
                if (!_descriptors.ContainsKey(descriptor.Path))
                {
                    _descriptors.Add(descriptor.Path, descriptor);
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
            foreach (var kvp in _descriptors)
            {
                kvp.Value.StartAddress = adr;
                adr += kvp.Value.EndAddress - kvp.Value.StartAddress;
            }
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

        public ImagesCollectionItem TryGetDescriptor(string path)
        {
            if (_descriptors.ContainsKey(path))
                return _descriptors[path];

            return null;
        }

        #endregion
    }
}
