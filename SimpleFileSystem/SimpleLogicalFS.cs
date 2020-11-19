// SimpleFS.cs
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
using System.Linq;


namespace SimpleFileSystem
{
    public class SimpleFS : FileSystem
    {
        #region filesystem

        //
        // File System
        //

        private const char PATH_SEPARATOR = FSConstants.PATH_SEPARATOR;
        private const int MAX_FILE_NAME = FSConstants.MAX_FILENAME;
        private const int BLOCK_SIZE = 500;     // 500 bytes... 2 sectors of 256 bytes each (minus sector overhead)

        private VirtualFS virtualFileSystem;

        public SimpleFS()
        {
            virtualFileSystem = new VirtualFS();
        }

        public void Mount(DiskDriver disk, string mountPoint)
        {
            virtualFileSystem.Mount(disk, mountPoint);
        }

        public void Unmount(string mountPoint)
        {
            virtualFileSystem.Unmount(mountPoint);
        }

        public void Format(DiskDriver disk)
        {
            virtualFileSystem.Format(disk);
        }

        public Directory GetRootDirectory()
        {
            //wraps virtual file systems root node as a simple directory object
            return new SimpleDirectory(virtualFileSystem.RootNode);
        }

        public FSEntry Find(string path)
        {
            // good:  /foo/bar, /foo/bar/
            // bad:  foo, foo/bar, //foo/bar, /foo//bar, /foo/../foo/bar
            VirtualNode current = virtualFileSystem.RootNode;
            string[] elements = path.Split(PATH_SEPARATOR);

            foreach (string element in elements.Skip(1))
            {
                VirtualNode child = current.GetChild(element);
                if (child != null)
                {
                    current = child;
                }
                else
                {
                    return null;
                }
            }

            FSEntry result = null;

            if (current.IsDirectory)
            {
                result = new SimpleDirectory(current);
            }
            else
            {
                result = new SimpleFile(current);
            }

            return result;
        }

        public char PathSeparator { get { return PATH_SEPARATOR; } }
        public int MaxNameLength { get { return MAX_FILE_NAME; } }

        #endregion

        #region implementation

        //
        // FSEntry
        //

        abstract private class SimpleEntry : FSEntry
        {
            protected VirtualNode node;

            protected SimpleEntry(VirtualNode node)
            {
                this.node = node;
            }

            public string Name => node.Name;
            public Directory Parent => node.Parent == null ? null : new SimpleDirectory(node.Parent);

            public string FullPathName
            {
                get
                {
                    string path = Name;
                    VirtualNode current = node.Parent;
                    while (current != null)
                    {
                        if (current.Name.Last() == FSConstants.PATH_SEPARATOR)
                        {
                            path = current.Name + path;
                        }
                        else
                        {
                            path = current.Name + FSConstants.PATH_SEPARATOR + path;
                        }
                        current = current.Parent;
                    }

                    return path;
                }
            }

            // override in derived classes
            public virtual bool IsDirectory => node.IsDirectory;
            public virtual bool IsFile => node.IsFile;

            public void Rename(string name)
            {
                node.Rename(name);
            }

            public void Move(Directory destination)
            {
                node.Move((destination as SimpleDirectory).node);
            }

            public void Delete()
            {
                node.Delete();
            }
        }

        //
        // Directory
        //

        private class SimpleDirectory : SimpleEntry, Directory
        {
            public SimpleDirectory(VirtualNode node) : base(node)
            {
            }

            public IEnumerable<Directory> GetSubDirectories()
            {
                List<Directory> result = new List<Directory>();

                foreach (VirtualNode child in node.GetChildren())
                {
                    if (child.IsDirectory)
                    {
                        result.Add(new SimpleDirectory(child));
                    }
                }

                return result;
            }

            public IEnumerable<File> GetFiles()
            {
                List<File> result = new List<File>();

                foreach (VirtualNode child in node.GetChildren())
                {
                    if (child.IsFile)
                    {
                        result.Add(new SimpleFile(child));
                    }
                }

                return result;
            }

            public Directory CreateDirectory(string name)
            {
                return new SimpleDirectory(node.CreateDirectoryNode(name));
            }

            public File CreateFile(string name)
            {
                return new SimpleFile(node.CreateFileNode(name));
            }
        }

        //
        // File
        //

        private class SimpleFile : SimpleEntry, File
        {
            public SimpleFile(VirtualNode node) : base(node)
            {
            }

            public int Length => node.FileLength;

            public FileStream Open()
            {
                return new SimpleStream(node);
            }

        }

        //
        // FileStream
        //

        private class SimpleStream : FileStream
        {
            private VirtualNode node;
            public SimpleStream(VirtualNode node)
            {
                this.node = node;
            }

            public void Close()
            {
                //remove access to file node, prevent future read/write calls on this stream
                node = null;
            }

            public byte[] Read(int index, int length)
            {
                //read length bytes from file node starting at index return byte area w/ data
                if (node == null)
                {
                    throw new Exception("Can't read from closed file stream");
                }
                return node.Read(index, length);
            }

            public void Write(int index, byte[] data)
            {
                //write data bytes to the file node, starting at index
                if (node == null)
                {
                    throw new Exception("Can't wirte to closed file stream");
                }
                node.Write(index, data);
            }
        }

        #endregion
    }
}
