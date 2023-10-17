// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net) and Silicon Studio Corp. (https://www.siliconstudio.co.jp)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.
using System;
using System.Runtime.InteropServices;
using System.Security;
using Stride.Core.Mathematics;
using SoLoud;

namespace Stride.Audio
{
    /// <summary>
    /// Wrapper around OpenAL
    /// </summary>
    public class AudioLayer
    {
        public struct Device
        {
            public Soloud Sl;
        }

        public struct Listener
        {
            // TODO: lol
            public Soloud Sl;
        }

        public struct Source
        {
            public Soloud Sl;
            public VoiceHandle Voice;
        }

        public struct Buffer
        {
            public Soloud Sl;
            public Wav Wav;
        }

        static AudioLayer()
        {
            NativeInvoke.PreLoad();
        }

        public static  bool Init() { return true; }

        public enum DeviceFlags
        {
            None,
            Hrtf,
        }

        public static Device Create(string deviceName, DeviceFlags flags)
        {
            var d = new Device { Sl = new Soloud() };
            d.Sl.Init();
            return d;
        }

        public static void Destroy(Device device)
        {
            device.Sl.Deinit();
        }

        public static void Update(Device device)
        {
            device.Sl.Update3DAudio();
        }

        public static void SetMasterVolume(Device device, float volume)
        {
            device.Sl.SetGlobalVolume(volume);
        }

        public static Listener ListenerCreate(Device device)
        {
            return new Listener { Sl = device.Sl };
        }

        public static void ListenerDestroy(Listener listener) { }

        public static bool ListenerEnable(Listener listener) { return true; }

        public static void ListenerDisable(Listener listener) { }

        [SuppressUnmanagedCodeSecurity]
        [DllImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceCreate", CallingConvention = CallingConvention.Cdecl)]
        public static extern Source SourceCreate(Listener listener, int sampleRate, int maxNumberOfBuffers, bool mono, bool spatialized, bool streamed, bool hrtf, float hrtfDirectionFactor, HrtfEnvironment environment);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceDestroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SourceDestroy(Source source);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceGetPosition", CallingConvention = CallingConvention.Cdecl)]
        public static extern double SourceGetPosition(Source source);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceSetPan", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SourceSetPan(Source source, float pan);

     //   [SuppressUnmanagedCodeSecurity]
       // [DllImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceSetReverb", CallingConvention = CallingConvention.Cdecl)]
        //public static extern void SourceSetReverb(Source source, float reverbLevel, float lpfDirect, float lpfReverb, float[] delayTimes);

        public static Buffer BufferCreate(int maxBufferSizeBytes)
        {
            var buffer = new Buffer();
            buffer.Wav = new Wav();
            return buffer;
        }

        public static void BufferDestroy(Buffer buffer)
        {
            buffer.Wav?.Dispose();
        }

        public static void BufferFill(Buffer buffer, IntPtr pcm, int bufferSize, int sampleRate, bool mono)
        {
            buffer.Wav.LoadMarshaledMemory(pcm, bufferSize);
        }

        [SuppressUnmanagedCodeSecurity]
        [DllImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceSetBuffer", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SourceSetBuffer(Source source, Buffer buffer);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceFlushBuffers", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SourceFlushBuffers(Source source);

        public enum BufferType
        {
            None,
            BeginOfStream,
            EndOfStream,
            EndOfLoop,
        }

        [SuppressUnmanagedCodeSecurity]
        [DllImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceQueueBuffer", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SourceQueueBuffer(Source source, Buffer buffer, IntPtr pcm, int bufferSize, BufferType streamType);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceGetFreeBuffer", CallingConvention = CallingConvention.Cdecl)]
        public static extern Buffer SourceGetFreeBuffer(Source source);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(NativeInvoke.Library, EntryPoint = "xnAudioSourcePlay", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SourcePlay(Source source);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(NativeInvoke.Library, EntryPoint = "xnAudioSourcePause", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SourcePause(Source source);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceStop", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SourceStop(Source source);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceSetLooping", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SourceSetLooping(Source source, bool looped);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceSetRange", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SourceSetRange(Source source, double startTime, double stopTime);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceSetGain", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SourceSetGain(Source source, float gain);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceSetPitch", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SourceSetPitch(Source source, float pitch);

        public static void ListenerPush3D(Listener listener, ref Vector3 pos, ref Vector3 forward, ref Vector3 up, ref Vector3 vel, ref Matrix worldTransform)
        {
            listener.Sl.Set3DListenerParameters(
                pos.X, pos.Y, pos.Z,
                //TODO: not sure what this vector does
                0, 0, 0,
                up.X, up.Y, up.Z,
                vel.X, vel.Y, vel.Z);
        }

        [SuppressUnmanagedCodeSecurity]
        [DllImport(NativeInvoke.Library, EntryPoint = "xnAudioSourcePush3D", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SourcePush3D(Source source, ref Vector3 pos, ref Vector3 forward, ref Vector3 up, ref Vector3 vel, ref Matrix worldTransform);

        [SuppressUnmanagedCodeSecurity]
        [DllImport(NativeInvoke.Library, EntryPoint = "xnAudioSourceIsPlaying", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool SourceIsPlaying(Source source);
    }
}
