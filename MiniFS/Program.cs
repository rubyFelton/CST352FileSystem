// Program.cs
// Oregon Institute of Technology
// Spring 2018
// Instructor: Pete Myers
//
//Ruby Felton
//
// NOTE: This Program was created from a stub handed out by instructor
// It was worked on collaboratively as a class 

using System;
using System.Collections.Generic;
using System.Text;
using SimpleFileSystem;


namespace MiniFS
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                TestDisks();
                TestPhysicalFileSystem();
                TestVirtualFileSystem();
                TestLogicalFileSystem();
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }

        #region disks

        static void TestDisks()
        {
            // Sample test for VolatileDisk
            VolatileDisk disk = new VolatileDisk(1);

            disk.TurnOn();

            byte[] testData = new byte[disk.BytesPerSector];
            for (int i = 0; i < disk.BytesPerSector; i++)
            {
                testData[i] = (byte)(i % 256);
            }

            TestSector(disk, 0, testData);
            TestSector(disk, 1, testData);
            TestSector(disk, disk.SectorCount - 1, testData);

            disk.TurnOff();
        }

        static bool TestSector(DiskDriver disk, int lba, byte[] testData)
        {
            // Sample test for VolatileDisk
            disk.WriteSector(lba, testData);
            byte[] s = disk.ReadSector(lba);
            bool success = Compare(testData, s);
            Console.WriteLine("Compare " + success.ToString());

            return success;
        }

        #endregion

        #region physical

        static void TestPhysicalFileSystem()
        {
            // try reading/writing various types of sectors
            // use CheckBytes() to compare what was written vs. read

            VolatileDisk disk = new VolatileDisk(1);
            disk.TurnOn();

            // write free sector to disk
            FREE_SECTOR freeSector1 = new FREE_SECTOR(disk.BytesPerSector);
            disk.WriteSector(0, freeSector1.RawBytes);

            //read sector
            byte[] bytes = disk.ReadSector(0);
            FREE_SECTOR freeSector2 = FREE_SECTOR.CreateFromBytes(bytes);

            //check if what was read is what was written
            CheckBytes("free_sector1", freeSector1, "freeSector2", freeSector2);

            // write drive info sector to disk
            DRIVE_INFO diSector1 = new DRIVE_INFO(disk.BytesPerSector, 42);
            disk.WriteSector(0, diSector1.RawBytes);

            //read sector
            byte[] bytes2 = disk.ReadSector(0);
            DRIVE_INFO diSector2 = DRIVE_INFO.CreateFromBytes(bytes2);

            //check if what was read is what was written
            CheckBytes("diSector1", diSector1, "diSector2", diSector2);

            // TODO: DIR_NODE

            // TODO: FILE_NODE

            // TODO: DATA_SECTOR

            disk.TurnOff();
        }

        #endregion

        #region virtual

        static void TestVirtualFileSystem()
        {
            try
            {
                Random r = new Random();

                VolatileDisk disk = new VolatileDisk(1);
                //PersistentDisk disk = new PersistentDisk(1, "disk1");
                disk.TurnOn();

                VirtualFS vfs = new VirtualFS();

                vfs.Format(disk);
                vfs.Mount(disk, "/");
                VirtualNode root = vfs.RootNode;

                VirtualNode dir1 = root.CreateDirectoryNode("dir1");
                VirtualNode dir2 = root.CreateDirectoryNode("dir2");

                VirtualNode file1 = dir1.CreateFileNode("file1");
                TestFileWriteRead(file1, r, 0, 100);    // 1 sector
                TestFileWriteRead(file1, r, 0, 500);    // 2 sectors
                TestFileWriteRead(file1, r, 250, 500);    // 3 sectors

                vfs.Unmount("/");

                vfs.Mount(disk, "/");
                RecursivelyPrintNodes(vfs.RootNode);

                disk.TurnOff();
            }
            catch (Exception ex)
            {
                Console.WriteLine("VFS test failed: " + ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void TestFileWriteRead(VirtualNode file, Random r, int index, int length)
        {
            byte[] towrite = CreateTestBytes(r, length);
            file.Write(index, towrite);
            byte[] toread = file.Read(index, length);
            if (!Compare(towrite, toread))
                throw new Exception("File read/write at " + index + " for " + length + " bytes, failed for file " + file.Name);
        }

        static void RecursivelyPrintNodes(VirtualNode node, string indent = "")
        {
            Console.Write(indent + node.Name);
            if (node.IsFile)
            {
                Console.WriteLine(" <file, len=" + node.FileLength.ToString() + ">");
            }
            else if (node.IsDirectory)
            {
                Console.WriteLine(" <directory>");
                foreach (VirtualNode child in node.GetChildren())
                {
                    RecursivelyPrintNodes(child, indent + "  ");
                }
            }
        }

        #endregion

        #region logical

        static void TestLogicalFileSystem()
        {
            //DiskDriver disk = new VolatileDisk(1);
            DiskDriver disk = new PersistentDisk(1, "disk1");
            disk.TurnOn();

            FileSystem fs = new SimpleFS();
            fs.Format(disk);
            fs.Mount(disk, "/");

            Directory root = fs.GetRootDirectory();

            Directory dir1 = root.CreateDirectory("dir1");
            Directory dir2 = root.CreateDirectory("dir2");

            Random r = new Random();
            byte[] bytes1 = CreateTestBytes(r, 1000);
            File file2 = dir2.CreateFile("file2");
            FileStream stream1 = file2.Open();
            stream1.Write(0, bytes1);
            stream1.Close();

            File file2_2 = (File)fs.Find("/dir2/file2");
            FileStream stream2 = file2_2.Open();
            byte[] bytes2 = stream2.Read(0, 1000);
            stream2.Close();
            if (!Compare(bytes1, bytes2))
                throw new Exception("bytes read were not the same as written");

            Console.WriteLine("Printing all directories...");
            RecursivelyPrintDirectories(root);
            Console.WriteLine();

            Console.WriteLine("Moving file2 to dir1...");
            file2.Move(dir1);

            Console.WriteLine("Printing all directories...");
            RecursivelyPrintDirectories(root);
            Console.WriteLine();

            Console.WriteLine("Renaming dir2 to renamed...");
            dir2.Rename("renamed");

            Console.WriteLine("Printing all directories...");
            RecursivelyPrintDirectories(root);
            Console.WriteLine();

            Console.WriteLine("Deleting renamed...");
            dir2.Delete();

            Console.WriteLine("Printing all directories...");
            RecursivelyPrintDirectories(root);
            Console.WriteLine();

            Console.WriteLine("Deleting dir1...");
            dir1.Delete();

            Console.WriteLine("Printing all directories...");
            RecursivelyPrintDirectories(root);
            Console.WriteLine();

            fs.Unmount("/");
            disk.TurnOff();
        }

        static void RecursivelyPrintDirectories(Directory dir, bool printFileContent = false, string indent = "")
        {
            Console.WriteLine(indent + dir.Name + " (directory " + dir.FullPathName + ")");
            foreach (Directory d in dir.GetSubDirectories())
            {
                RecursivelyPrintDirectories(d, printFileContent, indent + "  ");
            }
            foreach (File f in dir.GetFiles())
            {
                int len = f.Length;
                Console.WriteLine(indent + "  " + f.Name + $" (file, len = {len}) " + f.FullPathName);
                if (printFileContent)
                {
                    FileStream stream = f.Open();
                    byte[] content = stream.Read(0, len);
                    foreach (byte b in content)
                    {
                        Console.Write("0x{0:x2} ", b);
                    }
                    Console.WriteLine();
                }
            }
        }

        #endregion

        #region helpers

        static void CheckBytes(string name1, SECTOR s1, string name2, SECTOR s2)
        {
            // Helper method for testing if two sectors have exactly the same raw bytes
            if (!Compare(s1.RawBytes, s2.RawBytes))
                throw new Exception($"Sectors {name1} and {name2} are not equal!");
        }

        static byte[] CreateTestBytes(Random r, int length)
        {
            // Helper method for creating random test bytes
            byte[] result = new byte[length];
            r.NextBytes(result);
            return result;
        }

        static bool Compare(byte[] data1, byte[] data2)
        {
            // Helper method for comparing two byte arrays
            if (data1.Length != data2.Length)
                return false;

            for (int i = 0; i < data1.Length; i++)
            {
                if (data1[i] != data2[i])
                    return false;
            }

            return true;
        }

        #endregion
    }
}
