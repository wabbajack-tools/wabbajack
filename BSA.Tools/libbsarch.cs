using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BSA.Tools
{
    public class libbsarch
    {
        public struct bsa_archive_t { };
        public struct bsa_file_record_t { };
        public struct bsa_folder_record_t { };

        public unsafe delegate bool bsa_file_iteration_proc_t(bsa_archive_t archive, [MarshalAs(UnmanagedType.LPWStr)] string file_path, bsa_file_record_t *file_record, bsa_folder_record_t *folder_record, void* context);

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bsa_archive_t* bsa_create();


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
        public struct bsa_result_message_t
        {
            public byte code; // bsa_result_code_t

            public unsafe fixed byte text[1024 * 2];

        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
        public unsafe struct bsa_result_buffer_t
        {
            public UInt32 size;
            public IntPtr data;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
        public unsafe struct bsa_result_message_buffer_t
        {
            public bsa_result_buffer_t buffer;
            public bsa_result_message_t message;
        }

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bsa_result_message_t bsa_free(bsa_archive_t* t);

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bsa_result_message_t bsa_load_from_file(bsa_archive_t* archive, string file_path);


        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe UInt32 bsa_version_get(bsa_archive_t* archive);


        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe UInt32 bsa_file_count_get(bsa_archive_t* archive);

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bsa_result_message_t bsa_iterate_files(bsa_archive_t *archive, bsa_file_iteration_proc_t file_iteration_proc, void* context);

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bsa_result_message_buffer_t bsa_extract_file_data_by_filename(bsa_archive_t* archive, string file_path);

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bsa_result_message_t bsa_extract_file(bsa_archive_t* archive, string file_path, string save_as);

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bsa_result_message_t bsa_file_data_free(bsa_archive_t* archive, bsa_result_buffer_t file_data_result);

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bsa_archive_type_t bsa_archive_type_get(bsa_archive_t* archive);

        public enum bsa_archive_type_t : Int32
        {
            baNone, baTES3, baTES4, baFO3, baSSE, baFO4, baFO4dds
        }

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]

        public static extern unsafe bsa_result_message_t bsa_add_file_from_memory(bsa_archive_t* archive, string file_path, UInt32 size, byte* data);

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bsa_result_message_t bsa_save(bsa_archive_t* archive);

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bsa_result_message_t bsa_create_archive(bsa_archive_t* archive, string file_path, bsa_archive_type_t archive_type, bsa_entry_list_t* entry_list);


        // Entry Lists

        public struct bsa_entry_list_t { }

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bsa_entry_list_t* bsa_entry_list_create();

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bsa_result_message_t bsa_entry_list_free(bsa_entry_list_t* entry_list);

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe UInt32 bsa_entry_list_count(bsa_entry_list_t* entry_list);

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bsa_result_message_t bsa_entry_list_add(bsa_entry_list_t* entry_list, string entry_string);

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe UInt32 bsa_entry_list_get(bsa_entry_list_t* entry_list, UInt32 index, UInt32 string_buffer_size, string string_buffer);



        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe UInt32 bsa_archive_flags_get(bsa_archive_t* archive);

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe void bsa_archive_flags_set(bsa_archive_t* archive, UInt32 flags);

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe UInt32 bsa_file_flags_get(bsa_archive_t* archive);

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe void bsa_file_flags_set(bsa_archive_t* archive, UInt32 flags);

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bool bsa_compress_get(bsa_archive_t* archive);

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe void bsa_compress_set(bsa_archive_t* archive, bool flags);

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe bool bsa_share_data_get(bsa_archive_t* archive);

        [DllImport("libbsarch.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        public static extern unsafe void bsa_share_data_set(bsa_archive_t* archive, bool flags);


    }


}
