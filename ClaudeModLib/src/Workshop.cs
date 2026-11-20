using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Steamworks;
using UnityEngine;

namespace ClaudeModLib
{
    /// <summary>
    /// One Steam Workshop upload job: <c>claudemodlib_workshop\&lt;name&gt;.json</c> in the game's LocalLow folder.
    /// After the first upload the item id is written back into <see cref="publishedFileId"/>; later uploads update it.
    /// </summary>
    public class WorkshopJob
    {
        public string title = "";
        /// <summary>Path to a text file with the description (Steam BBCode). Relative paths: next to the job file.</summary>
        public string descriptionFile = "";
        /// <summary>Folder uploaded as the item: the mod root (desc.json, code\, assets\, content\ ...).</summary>
        public string contentFolder = "";
        /// <summary>Preview image (.png/.jpg, under 1 MB, square works best).</summary>
        public string previewImage = "";
        /// <summary>"private" (default), "friends" or "public".</summary>
        public string visibility = "private";
        public string changeNote = "";
        public string[] tags = new string[0];
        /// <summary>0 = not created yet.</summary>
        public ulong publishedFileId;
        /// <summary>Workshop ids of required items (e.g. ClaudeModLib for mods built on it).</summary>
        public ulong[] requiredItems = new ulong[0];

        [JsonIgnore] public string path;
        [JsonIgnore] public string name { get { return Path.GetFileNameWithoutExtension(path); } }

        public string Resolve(string p)
        {
            if (string.IsNullOrEmpty(p) || System.IO.Path.IsPathRooted(p)) return p;
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), p));
        }
    }

    /// <summary>
    /// Steam Workshop publisher using the game's own Steamworks.NET (runs inside the game, so the app id and the
    /// Steam login are the player's). Developer feature: only active when the jobs folder exists.
    /// The official 2018 "RoIWorkshopUploader" download is no longer available.
    /// </summary>
    public static class Workshop
    {
        public static string JobsFolder { get { return System.IO.Path.Combine(Application.persistentDataPath, "claudemodlib_workshop"); } }
        public static bool enabled { get { return Directory.Exists(JobsFolder); } }

        /// <summary>Human-readable state of the current/last upload.</summary>
        public static string status { get; private set; }
        public static bool busy { get; private set; }
        public static WorkshopJob current { get; private set; }

        static CallResult<CreateItemResult_t> _create;
        static CallResult<SubmitItemUpdateResult_t> _submit;
        static CallResult<AddUGCDependencyResult_t> _dependency;
        static UGCUpdateHandle_t _handle;
        static readonly Queue<ulong> _pendingDeps = new Queue<ulong>();

        public static List<WorkshopJob> LoadJobs()
        {
            var list = new List<WorkshopJob>();
            if (!enabled) return list;
            foreach (var f in Directory.GetFiles(JobsFolder, "*.json"))
            {
                try
                {
                    var j = JsonConvert.DeserializeObject<WorkshopJob>(File.ReadAllText(f));
                    j.path = f;
                    list.Add(j);
                }
                catch (Exception e) { Log.Warn("workshop job " + f + ": " + e.Message); }
            }
            return list;
        }

        /// <summary>Checks a job before uploading; returns null if OK, else the problem.</summary>
        public static string Validate(WorkshopJob j)
        {
            if (string.IsNullOrEmpty(j.title)) return "title is empty";
            var folder = j.Resolve(j.contentFolder);
            if (!Directory.Exists(folder)) return "contentFolder not found: " + folder;
            if (!File.Exists(System.IO.Path.Combine(folder, "desc.json"))) return "contentFolder has no desc.json: " + folder;
            var prev = j.Resolve(j.previewImage);
            if (!string.IsNullOrEmpty(prev))
            {
                if (!File.Exists(prev)) return "previewImage not found: " + prev;
                if (new FileInfo(prev).Length >= 1024 * 1024) return "previewImage must be under 1 MB";
            }
            var desc = j.Resolve(j.descriptionFile);
            if (!string.IsNullOrEmpty(desc) && !File.Exists(desc)) return "descriptionFile not found: " + desc;
            return null;
        }

        /// <summary>Creates the item if needed, then uploads content, preview, texts and visibility.</summary>
        public static void Publish(WorkshopJob job)
        {
            if (busy) return;
            var problem = Validate(job);
            if (problem != null) { status = problem; return; }
            current = job; busy = true;
            try
            {
                if (job.publishedFileId == 0)
                {
                    status = "Creating Workshop item…";
                    if (_create == null) _create = CallResult<CreateItemResult_t>.Create(OnCreated);
                    _create.Set(SteamUGC.CreateItem(SteamUtils.GetAppID(), EWorkshopFileType.k_EWorkshopFileTypeCommunity));
                }
                else StartUpdate();
            }
            catch (Exception e) { Fail("Steam not available: " + e.Message); }
        }

        static void OnCreated(CreateItemResult_t r, bool ioFailure)
        {
            if (ioFailure || r.m_eResult != EResult.k_EResultOK) { Fail("CreateItem failed: " + r.m_eResult); return; }
            current.publishedFileId = r.m_nPublishedFileId.m_PublishedFileId;
            SaveJob(current);
            Log.Info("workshop item created: " + current.publishedFileId);
            if (r.m_bUserNeedsToAcceptWorkshopLegalAgreement)
                Log.Warn("Steam: accept the Workshop legal agreement on the item page, otherwise it stays hidden.");
            StartUpdate();
        }

        static void StartUpdate()
        {
            var j = current;
            _handle = SteamUGC.StartItemUpdate(SteamUtils.GetAppID(), new PublishedFileId_t(j.publishedFileId));
            SteamUGC.SetItemTitle(_handle, j.title);
            var desc = j.Resolve(j.descriptionFile);
            if (!string.IsNullOrEmpty(desc)) SteamUGC.SetItemDescription(_handle, File.ReadAllText(desc));
            SteamUGC.SetItemContent(_handle, j.Resolve(j.contentFolder));
            var prev = j.Resolve(j.previewImage);
            if (!string.IsNullOrEmpty(prev)) SteamUGC.SetItemPreview(_handle, prev);
            SteamUGC.SetItemVisibility(_handle, Visibility(j.visibility));
            if (j.tags != null && j.tags.Length > 0) SteamUGC.SetItemTags(_handle, j.tags);
            status = "Uploading…";
            if (_submit == null) _submit = CallResult<SubmitItemUpdateResult_t>.Create(OnSubmitted);
            _submit.Set(SteamUGC.SubmitItemUpdate(_handle, string.IsNullOrEmpty(j.changeNote) ? null : j.changeNote));
        }

        static void OnSubmitted(SubmitItemUpdateResult_t r, bool ioFailure)
        {
            if (ioFailure || r.m_eResult != EResult.k_EResultOK) { Fail("SubmitItemUpdate failed: " + r.m_eResult); return; }
            Log.Info("workshop item updated: " + current.publishedFileId);
            _pendingDeps.Clear();
            if (current.requiredItems != null) foreach (var d in current.requiredItems) if (d != 0) _pendingDeps.Enqueue(d);
            if (r.m_bUserNeedsToAcceptWorkshopLegalAgreement)
                status = "Uploaded - accept the Workshop legal agreement on the item page!";
            NextDependency();
        }

        // "Required items" on the Workshop page (subscribers get prompted to subscribe to them too).
        static void NextDependency()
        {
            if (_pendingDeps.Count == 0)
            {
                busy = false;
                if (status == null || !status.StartsWith("Uploaded -")) status = "Done: item " + current.publishedFileId + " (" + current.visibility + ")";
                return;
            }
            var child = _pendingDeps.Dequeue();
            if (_dependency == null) _dependency = CallResult<AddUGCDependencyResult_t>.Create((r, io) =>
            {
                if (io || r.m_eResult != EResult.k_EResultOK) Log.Warn("AddDependency " + r.m_nChildPublishedFileId + ": " + r.m_eResult);
                NextDependency();
            });
            status = "Linking required item " + child + "…";
            _dependency.Set(SteamUGC.AddDependency(new PublishedFileId_t(current.publishedFileId), new PublishedFileId_t(child)));
        }

        /// <summary>Upload progress 0..1 (or -1 when unknown), for the panel.</summary>
        public static float progress
        {
            get
            {
                if (!busy || current == null || current.publishedFileId == 0) return -1f;
                ulong done, total;
                try
                {
                    SteamUGC.GetItemUpdateProgress(_handle, out done, out total);
                    return total > 0 ? (float)done / total : -1f;
                }
                catch { return -1f; }
            }
        }

        /// <summary>Steam page of an item.</summary>
        public static string ItemUrl(ulong id) { return "https://steamcommunity.com/sharedfiles/filedetails/?id=" + id; }

        static ERemoteStoragePublishedFileVisibility Visibility(string v)
        {
            switch ((v ?? "").ToLowerInvariant())
            {
                case "public": return ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic;
                case "friends": return ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly;
                default: return ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate;
            }
        }

        static void SaveJob(WorkshopJob j)
        {
            try { File.WriteAllText(j.path, JsonConvert.SerializeObject(j, Formatting.Indented)); }
            catch (Exception e) { Log.Warn("could not save " + j.path + ": " + e.Message); }
        }

        static void Fail(string msg) { status = msg; busy = false; Log.Warn("workshop: " + msg); }
    }
}
