using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Stride.Audio
{
    public class AudioBusController
    {
        public static AudioBusController Inst;
        public AudioBusController()
        {
            Inst = this;
        }

        public float MasterVolume = 1f;
        public float SfxVolume = 1f;
        public float MusicVolume = 1f;
        public float UIVolume = 1f;

        public event Action<float> SfxChanged;
        public event Action<float> MusicChanged;
        public event Action<float> UIChanged;

        public float GetVolume(AudioBus bus)
        {
            switch (bus)
            {
                default:
                case AudioBus.None: 
                    return MasterVolume;
                case AudioBus.SFX:
                    return MasterVolume * SfxVolume;
                case AudioBus.Music: 
                    return MasterVolume * MusicVolume;
                case AudioBus.UI: 
                    return MasterVolume * UIVolume;
            }
        }

        public void SetMaster(float masterVolume)
        {
            MasterVolume = masterVolume;
            SfxChanged?.Invoke(masterVolume * SfxVolume);
            MusicChanged?.Invoke(masterVolume * MusicVolume);
            UIChanged?.Invoke(masterVolume * UIVolume);
        }

        public void SetSfx(float v)
        {
            SfxVolume = v;
            SfxChanged?.Invoke(MasterVolume * SfxVolume);
        }

        public void SetMusic(float v)
        {
            MusicVolume = v;
            MusicChanged?.Invoke(MasterVolume * v);
        }

        public void SetUI(float v)
        {
            UIVolume = v;
            UIChanged?.Invoke(MasterVolume * UIVolume);
        }
        
        public float Dry = 1f;
        public float Wet = 0f;

        public void SetDry(float v)
        {
            Dry = v;
        }

        public void SetWet(float v)
        {
            Wet = v;
        }
    }
}
