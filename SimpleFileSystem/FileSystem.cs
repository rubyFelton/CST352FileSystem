// FileSystem.cs
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


namespace SimpleFileSystem
{
    public interface FileSystem
    {
        // formats a disk to make it ready for the file system
        // overwrites any existing data/structure on the disk
        // disks must be formatted before mounting
        void Format(DiskDriver disk);

        // at least one disk must be mounted
        // first mounted disk contains the root directory and should have the root directory as mount point, e.g. "/"
        // subsequent disk mount points are expected to be non-empty paths from root, e.g. "/disk2"
        void Mount(DiskDriver disk, string mountPoint);
        void Unmount(string mountPoint);

        // path spearator is the name of the root directory, and is used to separate names in a path, e.g. '/' and "/foo/bar"
        char PathSeparator { get; }

        // each directory or file name must not be longer than this length
        // the full path to a directory or file, may be many multiples of this length
        // e.g. if max is 3, then "/a/bb/ccc" is valid, but "/a/bb/dddd" is not valid
        int MaxNameLength { get; }

        // the root directory is on the first mounted drive, and is always named the path separator, e.g. "/"
        Directory GetRootDirectory();

        // searches for an entry, starting at the root directory
        // path must be a fully qualified path, e.g. "/foo/bar" rather than just "foo/bar" or "bar"
        FSEntry Find(string path);
    }

    public interface FSEntry
    {
        // a directory or file in the file system
        string Name { get; }
        string FullPathName { get; }
        bool IsDirectory { get; }
        bool IsFile { get; }
        Directory Parent { get; }       // null for root directory

        void Rename(string name);
        void Move(Directory destination);
        void Delete();
    }

    public interface Directory : FSEntry
    {
        IEnumerable<Directory> GetSubDirectories();
        IEnumerable<File> GetFiles();
        Directory CreateDirectory(string name);
        File CreateFile(string name);
    }

    public interface File : FSEntry
    {
        int Length { get; }

        FileStream Open();
    }

    public interface FileStream
    {
        void Close();
        byte[] Read(int index, int length);
        void Write(int index, byte[] data);
    }
}
