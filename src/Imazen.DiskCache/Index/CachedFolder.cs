/* Copyright (c) 2014 Imazen See license.txt for your rights. */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Imazen.DiskCache.Index {
    internal delegate void FileDisappearedHandler(string relativePath, string physicalPath);

    /// <summary>
    /// Represents a cached view of a folder of cached items
    /// </summary>
    internal class CachedFolder {
        protected CachedFolder() { }

        private readonly object sync = new object();

        private volatile bool isValid;
        /// <summary>
        /// Defaults to false. Set to true immediately after being refreshed from the file system.
        /// Set to false if a file disappears from the file system cache without the cache index being notified first.
        /// Used by the cleanup system - not of importance to the cache write system.
        /// </summary>
        private bool IsValid {
            get => isValid;
            set => isValid = value;
        }




        /// <summary>
        /// Fired when a file disappears from the cache folder without the cache index knowing about it.
        /// </summary>
        public event FileDisappearedHandler FileDisappeared;

        private static StringComparer KeyComparer => StringComparer.OrdinalIgnoreCase;


        private Dictionary<string, CachedFolder> folders = new Dictionary<string, CachedFolder>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, CachedFileInfo> files = new Dictionary<string, CachedFileInfo>(StringComparer.OrdinalIgnoreCase);


        private void Clear() {
            lock (sync) {
                IsValid = false;
                folders.Clear();
                files.Clear();
            }
        }

        /// <summary>
        /// Returns null if (a) the file doesn't exist, or (b) the file isn't populated. Calling code should always fall back to file system calls on a null result.
        /// </summary>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        public CachedFileInfo GetCachedFileInfo(string relativePath) {
            relativePath = CheckRelativePath(relativePath);
            lock (sync) {
                int slash = relativePath.IndexOf('/');
                if (slash < 0) {
                    if (files.TryGetValue(relativePath, out var f)) return f; //cache hit
                } else {
                    //Try to access subfolder
                    string folder = relativePath.Substring(0, slash);
                    folders.TryGetValue(folder, out var f);
                    //Recurse if possible
                    if (f != null) return f.GetCachedFileInfo(relativePath.Substring(slash + 1));
                }
                return null; //cache miss or file not found
            }
        }

        /// <summary>
        /// Sets the CachedFileInfo object for the specified path, creating any needed folders along the way.
        /// If 'null', the item will be removed, and no missing folder will be created.
        /// </summary>
        /// <param name="relativePath"></param>
        /// <param name="info"></param>
        public void SetCachedFileInfo(string relativePath, CachedFileInfo info) {
            relativePath = CheckRelativePath(relativePath);
            lock (sync) {
                int slash = relativePath.IndexOf('/');
                if (slash < 0) {
                    //Set or remove the file
                    if (info == null)
                        files.Remove(relativePath);
                    else
                        files[relativePath] = info;
                } else {
                    //Try to access subfolder
                    string folder = relativePath.Substring(0, slash);
                    folders.TryGetValue(folder, out var f);
                    
                    if (info == null && f == null) return; //If the folder doesn't exist, the file definitely doesn't. Already accomplished.
                    //Create it if it doesn't exist
                    if (f == null) f = folders[folder] = new CachedFolder();
                    //Recurse if possible
                    f.SetCachedFileInfo(relativePath.Substring(slash + 1), info);
                }
            }
        }
        /// <summary>
        /// Tries to set the AccessedUtc of the specified file to the current date (just in memory, not on the file system).
        /// </summary>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        public bool BumpDateIfExists(string relativePath) {
            relativePath = CheckRelativePath(relativePath);
            lock (sync) {
                int slash = relativePath.IndexOf('/');
                if (slash < 0) {
                    //Update the accessed date.
                    if (files.TryGetValue(relativePath, out var old))
                    {
                        files[relativePath] = new CachedFileInfo(old, DateTime.UtcNow);
                    }
                    return true; //We updated it!
                } else {
                    //Try to access subfolder
                    string folder = relativePath.Substring(0, slash);
                    if (!folders.TryGetValue(folder, out var f)) return false;//If the folder doesn't exist, quit
                    if (f == null) return false; //If the folder is null, quit!

                    //Recurse if possible
                    return f.BumpDateIfExists(relativePath.Substring(slash + 1));
                }
            }
        }

        /// <summary>
        /// Gets a CachedFileInfo object for the file even if it isn't in the cache (falls back to the file system)
        /// </summary>
        /// <param name="relativePath"></param>
        /// <param name="physicalPath"></param>
        /// <returns></returns>
        private CachedFileInfo GetFileInfo(string relativePath, string physicalPath) {
            relativePath = CheckRelativePath(relativePath);
            lock (sync) {
                CachedFileInfo f = GetCachedFileInfo(relativePath);
                //On cache miss or no file
                if (f == null && File.Exists(physicalPath)) {
                    //on cache miss
                    f = new CachedFileInfo(new FileInfo(physicalPath));
                    //Populate cache
                    SetCachedFileInfo(relativePath, f);
                }
                return f;//Null only if the file doesn't exist.
            }
        }
        /// <summary>
        /// Verifies the file exists before returning the cached data. 
        /// Discrepancies in file existence result in OnFileDisappeared being fired.
        /// </summary>
        /// <param name="relativePath"></param>
        /// <param name="physicalPath"></param>
        /// <returns></returns>
        private CachedFileInfo GetFileInfoCertainExists(string relativePath, string physicalPath) {
            relativePath = CheckRelativePath(relativePath);
            bool fireEvent = false;
            CachedFileInfo f;
            lock (sync) {
                bool exists = File.Exists(physicalPath);

                f = GetCachedFileInfo(relativePath);
                //cache miss
                if (f == null && exists) {
                    //on cache miss
                    f = new CachedFileInfo(new FileInfo(physicalPath));
                    //Populate cache
                    SetCachedFileInfo(relativePath, f);
                }
                //cache wrong, discrepancy. File deleted by external actor
                if (f != null && !exists) {
                    f = null;
                    Clear(); //Clear the cache completely.
                    fireEvent = true;
                }
            }
            //Fire the event outside of the lock.
            if (fireEvent) FileDisappeared?.Invoke(relativePath, physicalPath);

            return f;//Null only if the file doesn't exist.
        }

        /// <summary>
        /// Returns the value of IsValid on the specified folder if present, or 'false' if not present.
        /// </summary>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        public bool GetIsValid(string relativePath) {
            lock (sync) {
                CachedFolder f = GetFolder(relativePath);
                if (f != null) return f.IsValid;
                return false;
            }
        }
        /// <summary>
        /// Not thread safe.
        /// </summary>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        private CachedFolder GetFolder(string relativePath) {
            return GetOrCreateFolder(relativePath, false);
        }

        private CachedFolder GetOrCreateFolder(string relativePath, bool createIfMissing) {
            relativePath = CheckRelativePath(relativePath);
            if (string.IsNullOrEmpty(relativePath)) return this;

            int slash = relativePath.IndexOf('/');
            string folder = relativePath;
            if (slash > -1) {
                folder = relativePath.Substring(0, slash);
                relativePath = relativePath.Substring(slash + 1);
            } else relativePath = "";

            CachedFolder f;
            if (!folders.TryGetValue(folder, out f)) {
                if (!createIfMissing) return null;
                else f = folders[folder] = new CachedFolder();
            }
            //Recurse if possible
            return f?.GetFolder(relativePath);
            //Not found
        }

        /// <summary>
        /// returns a list 
        /// </summary>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        public IList<string> GetSubfolders(string relativePath) {
            lock (sync) {
                CachedFolder f = GetFolder(relativePath);
                return new List<string>(f.folders.Keys);
            }
        }
        
        /// <summary>
        /// returns a dictionary of files. 
        /// </summary>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        public ICollection<KeyValuePair<string, CachedFileInfo>> GetSortedSubFiles(string relativePath) {
            lock (sync) {
                CachedFolder f = GetFolder(relativePath);
                if (f == null || f.files.Count < 1) return null;
                //Copy pairs to an array.
                KeyValuePair<string, CachedFileInfo>[] items = new KeyValuePair<string, CachedFileInfo>[f.files.Count];
                int i = 0;
                foreach (KeyValuePair<string, CachedFileInfo> pair in f.files) {
                    items[i] = pair;
                    i++;
                }
                //Sort the pairs on accessed date
                Array.Sort(items,
                    (a, b) => 
                        DateTime.Compare(a.Value.AccessedUtc, b.Value.AccessedUtc));


                return items;
            }
        }


        public int GetFileCount(string relativePath) {
            lock (sync) {
                CachedFolder f = GetFolder(relativePath);
                return f.files.Count;
            }
        }
        /// <summary>
        /// Refreshes file and folder listing for this folder (non-recursive). Sets IsValid=true afterwards.
        /// 
        /// </summary>
        /// <param name="relativePath"></param>
        /// <param name="physicalPath"></param>
        public void Populate(string relativePath, string physicalPath) {
            //NDJ-added May 29,2011
            //Nothing was setting IsValue=true before.
            PopulateSubfolders(relativePath, physicalPath);
            PopulateFiles(relativePath, physicalPath);
            GetOrCreateFolder(relativePath, true).IsValid = true;
        }
        /// <summary>
        /// Updates  the 'folders' dictionary to match the folders that exist on disk. ONLY UPDATES THE LOCAL FOLDER
        /// </summary>
        /// <param name="relativePath"></param>
        /// <param name="physicalPath"></param>
        private void PopulateSubfolders(string relativePath, string physicalPath) {
            relativePath = CheckRelativePath(relativePath);
            string[] dirs;
            try {
                 dirs = Directory.GetDirectories(physicalPath);
            } catch (DirectoryNotFoundException) {
                dirs = new string[]{}; //Pretend it's empty. We don't care, the next recursive will get rid of it.
            }
            lock (sync) {
                CachedFolder f = GetOrCreateFolder(relativePath, true);
                Dictionary<string, CachedFolder> newFolders = new Dictionary<string, CachedFolder>(dirs.Length, KeyComparer);
                foreach (string s in dirs) {
                    string local = s.Substring(s.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                    if (local.StartsWith(".")) continue; //Skip folders that start with a period.
                    if (f.folders.ContainsKey(local)) 
                        newFolders[local] = f.folders[local]; //What if the value is null? Does ContainsKey work?
                    else 
                        newFolders[local] = new CachedFolder();
                }
                f.folders = newFolders; //Question - why didn't the folders get listed?
            }
        }
        /// <summary>
        /// Updates the 'files' dictionary to match the files that exist on disk. Uses the accessedUtc values from the previous dictionary if they are newer.
        /// </summary>
        /// <param name="relativePath"></param>
        /// <param name="physicalPath"></param>
        private void PopulateFiles(string relativePath, string physicalPath) {
            relativePath = CheckRelativePath(relativePath);
            string[] physicalFiles;
            try {
                physicalFiles = Directory.GetFiles(physicalPath);
            } catch (DirectoryNotFoundException) {
                physicalFiles = new string[] { }; //Pretend it's empty. We don't care, the next recursive will get rid of it.
            }
            Dictionary<string, CachedFileInfo> newFiles = new Dictionary<string, CachedFileInfo>(physicalFiles.Length, KeyComparer);

            CachedFolder f = GetOrCreateFolder(relativePath, true);
            foreach (string s in physicalFiles) {
                string local = s.Substring(s.LastIndexOf(Path.DirectorySeparatorChar) + 1);
                
                //Todo, add a callback that handles exclusion of files
                if (local.EndsWith(".config", StringComparison.OrdinalIgnoreCase)) continue;
                if (local.StartsWith(".")) continue; //Skip files that start with a period

                //What did we have on file?
                CachedFileInfo old;
                lock (sync) {
                    if (!f.files.TryGetValue(relativePath, out old))
                    {
                    }
                }
                newFiles[local] = new CachedFileInfo(new FileInfo(s), old);
            }
            lock (sync) {
                f.files = newFiles;
            }
        }



        public bool ExistsCertain(string relativePath, string physicalPath) {
            return GetFileInfoCertainExists(relativePath, physicalPath) != null;
        }
        public bool Exists(string relativePath, string physicalPath) {
            return GetFileInfo(relativePath, physicalPath) != null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="relativePath"></param>
        /// <returns></returns>
        private static string CheckRelativePath(string relativePath) {
            if (relativePath == null) return null;
            if (relativePath.StartsWith("/") || relativePath.EndsWith("/")) {
                Debug.WriteLine("Invalid relativePath value - should never have leading slash!");
            }
            return relativePath;
        }
    }

}
