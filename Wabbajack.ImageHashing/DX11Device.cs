using System;
using System.Runtime.InteropServices;
using DirectXTexNet;
using Silk.NET.Core.Native;
using Silk.NET.Direct3D11;

namespace Wabbajack.ImageHashing
{
    public unsafe class DX11Device
    {
        private ID3D11Device* _device;

        public DX11Device()
        {
            unsafe
            {
                var dxgi = Silk.NET.DXGI.DXGI.GetApi();
                var dx11 = Silk.NET.Direct3D11.D3D11.GetApi();

                D3DFeatureLevel[] levels =
                {
                    //D3DFeatureLevel.D3DFeatureLevel100, D3DFeatureLevel.D3DFeatureLevel101,
                    D3DFeatureLevel.D3DFeatureLevel110
                };
                uint createDeviceFlags = 0;

                var adapterIdx = 0;

                D3DFeatureLevel fl;
                ID3D11Device* device;
                fixed (D3DFeatureLevel* lvls = levels)
                {
                    var hr = dx11.CreateDevice(null, D3DDriverType.D3DDriverTypeHardware, IntPtr.Zero,
                        createDeviceFlags, lvls,
                        (uint)levels.Length,
                        Silk.NET.Direct3D11.D3D11.SdkVersion, &device, &fl, null);

                    if (FAILED(hr))
                    {
                        _device = null;
                        return;
                    }

                    _device = device;
                }
            }
        }

        public ScratchImage Compress(ScratchImage input, DXGI_FORMAT format, TEX_COMPRESS_FLAGS compress,
            float threshold)
        {
            lock (this)
            {
                if (_device != null)
                {
                    try
                    {
                        return input.Compress((IntPtr)_device, format, compress, threshold);
                    }
                    catch (COMException _)
                    {
                        _device->Release();
                        _device = null;
                    }
                }
                return input.Compress(format, compress, threshold);
            }
        }

        private static bool FAILED(int x)
        {
            return x != 0;
        }
    }
}
