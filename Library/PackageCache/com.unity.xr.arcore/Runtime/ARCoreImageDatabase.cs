using System;
using System.Runtime.InteropServices;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine.Assertions;
using UnityEngine.Networking;
using UnityEngine.XR.ARSubsystems;

namespace UnityEngine.XR.ARCore
{
    sealed class ARCoreImageDatabase : MutableRuntimeReferenceImageLibrary
    {
        IntPtr m_Self;

        internal const string dataStoreKey = "com.unity.xr.arcore";

        struct AddImageJob : IJob
        {
            [DeallocateOnJobCompletion]
            public NativeArray<byte> grayscaleImage;

            [DeallocateOnJobCompletion]
            public NativeArray<byte> name;

            public IntPtr database;

            public IntPtr validator;

            public int width;

            public int height;

            public ManagedReferenceImage managedReferenceImage;

            public unsafe void Execute()
            {
                AddImage(database, validator, ref managedReferenceImage,
                         grayscaleImage.GetUnsafePtr(), width, height, name.GetUnsafePtr());

                if (!GetStatus(validator).IsSuccess())
                {
                    managedReferenceImage.Dispose();
                }

                RcoApi.Release(database);
            }

            [DllImport(Constants.k_LibraryName, EntryPoint = "UnityARCore_ImageDatabase_AddImage")]
            static extern unsafe void AddImage(
                IntPtr database, IntPtr validator,
                ref ManagedReferenceImage managedReferenceImage,
                void* image, int width, int height, void* name);
        }

        static byte[] GetLibraryData(XRReferenceImageLibrary library)
        {
            // 4.2+
            if (library.dataStore.TryGetValue(dataStoreKey, out var bytes))
            {
                return bytes;
            }

            // Pre 4.2 fallback
            using var webRequest = new UnityWebRequest(ARCoreImageTrackingSubsystem.GetPathForLibrary(library))
            {
                downloadHandler = new DownloadHandlerBuffer(),
                disposeDownloadHandlerOnDispose = true
            };

            webRequest.SendWebRequest();
            while (!webRequest.isDone) { }

            return webRequest.downloadHandler.data;
        }

        public unsafe ARCoreImageDatabase(XRReferenceImageLibrary serializedLibrary)
        {
            if (serializedLibrary == null)
            {
                m_Self = Deserialize(default, default);
            }
            else
            {
                var libraryBlob = GetLibraryData(serializedLibrary);
                if (libraryBlob == null || libraryBlob.Length == 0)
                {
                    m_Self = Deserialize(default, default);
                    Debug.LogError($"Failed to load {nameof(XRReferenceImageLibrary)} '{serializedLibrary.name}': library does not contain any ARCore data.");
                    return;
                }

                using var managedReferenceImages = serializedLibrary.ToNativeArray(Allocator.Temp);
                fixed (byte* ptr = libraryBlob)
                {
                    m_Self = Deserialize(new NativeView
                    {
                        ptr = ptr,
                        count = libraryBlob.Length
                    }, managedReferenceImages.AsNativeView());
                }
            }
        }

        public static explicit operator IntPtr(ARCoreImageDatabase database) => database.m_Self;

        ~ARCoreImageDatabase()
        {
            Assert.AreNotEqual(m_Self, IntPtr.Zero);

            // Release references
            int n = count;
            for (int i = 0; i < n; ++i)
            {
                GetReferenceImage(m_Self, i).Dispose();
            }
            RcoApi.Release(m_Self);
        }

        static readonly TextureFormat[] k_SupportedFormats =
        {
            TextureFormat.Alpha8,
            TextureFormat.R8,
            TextureFormat.RFloat,
            TextureFormat.RGB24,
            TextureFormat.RGBA32,
            TextureFormat.ARGB32,
            TextureFormat.BGRA32,
        };

        public override bool supportsValidation => true;

        public override int supportedTextureFormatCount => k_SupportedFormats.Length;

        protected override TextureFormat GetSupportedTextureFormatAtImpl(int index) => k_SupportedFormats[index];

        protected override AddReferenceImageJobStatus GetAddReferenceImageJobStatus(AddReferenceImageJobState state) =>
            GetStatus(state.AsIntPtr());

        unsafe NativeArray<byte> GetUTF8Bytes(string s)
        {
            var byteCount = Encoding.UTF8.GetByteCount(s);
            var utf8Bytes = new NativeArray<byte>(byteCount + 1, Allocator.Persistent);
            fixed (char* chars = s)
            {
                try
                {
                    Encoding.UTF8.GetBytes(chars, s.Length, (byte*)utf8Bytes.GetUnsafePtr(), byteCount);
                }
                catch
                {
                    utf8Bytes.Dispose();
                    throw;
                }
            }

            return utf8Bytes;
        }

        protected override AddReferenceImageJobState ScheduleAddImageWithValidationJobImpl(NativeSlice<byte> imageBytes,
            Vector2Int sizeInPixels, TextureFormat format, XRReferenceImage referenceImage, JobHandle inputDeps)
        {
            var grayscaleImage = new NativeArray<byte>(
                sizeInPixels.x * sizeInPixels.y,
                Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);

            inputDeps = ConversionJob.Schedule(imageBytes, sizeInPixels, format, grayscaleImage, inputDeps);

            // Add a reference in case we are destroyed while the job is running
            RcoApi.Retain(m_Self);

            var validator = CreateValidator(m_Self);

            inputDeps = new AddImageJob
            {
                database = m_Self,
                validator = validator,
                managedReferenceImage = new ManagedReferenceImage(referenceImage),
                grayscaleImage = grayscaleImage,
                width = sizeInPixels.x,
                height = sizeInPixels.y,
                name = GetUTF8Bytes(referenceImage.name),
            }.Schedule(inputDeps);

            return CreateAddJobState(validator, inputDeps);
        }

        protected override JobHandle ScheduleAddImageJobImpl(
            NativeSlice<byte> imageBytes,
            Vector2Int sizeInPixels,
            TextureFormat format,
            XRReferenceImage referenceImage,
            JobHandle inputDeps) =>
            ScheduleAddImageWithValidationJobImpl(imageBytes, sizeInPixels, format, referenceImage, inputDeps).jobHandle;

        protected override XRReferenceImage GetReferenceImageAt(int index)
        {
            Assert.AreNotEqual(m_Self, IntPtr.Zero);
            return GetReferenceImage(m_Self, index).ToReferenceImage();
        }

        public override int count
        {
            get
            {
                Assert.AreNotEqual(m_Self, IntPtr.Zero);
                return GetReferenceImageCount(m_Self);
            }
        }

        public override int GetHashCode() => m_Self.GetHashCode();
        public override bool Equals(object obj) => (obj is ARCoreImageDatabase) && Equals((ARCoreImageDatabase)obj);
        public bool Equals(ARCoreImageDatabase other) => !ReferenceEquals(other, null) && (m_Self == other.m_Self);
        public static bool operator ==(ARCoreImageDatabase lhs, ARCoreImageDatabase rhs)
        {
            if (ReferenceEquals(lhs, rhs))
                return true;

            if (ReferenceEquals(lhs, null) || ReferenceEquals(rhs, null))
                return false;

            return lhs.Equals(rhs);
        }
        public static bool operator !=(ARCoreImageDatabase lhs, ARCoreImageDatabase rhs) => !(lhs == rhs);

        [DllImport(Constants.k_LibraryName, EntryPoint = "UnityARCore_ImageDatabase_GetReferenceImage")]
        static extern ManagedReferenceImage GetReferenceImage(IntPtr self, int index);

        [DllImport(Constants.k_LibraryName, EntryPoint = "UnityARCore_ImageDatabase_GetReferenceImageCount")]
        static extern int GetReferenceImageCount(IntPtr self);

        [DllImport(Constants.k_LibraryName, EntryPoint = "UnityARCore_ImageDatabase_Deserialize")]
        static extern IntPtr Deserialize(NativeView serializedDatabase, NativeView referenceImages);

        [DllImport(Constants.k_LibraryName, EntryPoint = "UnityARCore_ImageDatabase_CreateValidator")]
        static extern IntPtr CreateValidator(IntPtr self);

        [DllImport(Constants.k_LibraryName, EntryPoint = "UnityARCore_ReferenceImageValidator_GetStatus")]
        static extern AddReferenceImageJobStatus GetStatus(IntPtr validator);
    }
}
