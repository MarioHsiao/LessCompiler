﻿using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO;
using System.Threading.Tasks;

namespace LessCompiler
{
    public static class VsHelpers
    {
        public static DTE2 DTE = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2;
        private static IVsStatusbar statusbar = (IVsStatusbar)ServiceProvider.GlobalProvider.GetService(typeof(SVsStatusbar));

        public static void WriteStatus(string text)
        {
            statusbar.FreezeOutput(0);
            statusbar.SetText(text);
            statusbar.FreezeOutput(1);
        }

        public static void CheckFileOutOfSourceControl(string file)
        {
            if (!File.Exists(file) || DTE.Solution.FindProjectItem(file) == null)
                return;

            if (DTE.SourceControl.IsItemUnderSCC(file) && !DTE.SourceControl.IsItemCheckedOut(file))
                DTE.SourceControl.CheckOutItem(file);

            var info = new FileInfo(file)
            {
                IsReadOnly = false
            };
        }

        public static void AddFileToProject(this Project project, string file, string itemType = null)
        {
            if (project.IsKind(ProjectTypes.ASPNET_5, ProjectTypes.DOTNET_Core, ProjectTypes.SSDT))
                return;

            try
            {
                if (DTE.Solution.FindProjectItem(file) == null)
                {
                    ProjectItem item = project.ProjectItems.AddFromFile(file);

                    if (string.IsNullOrEmpty(itemType)
                        || project.IsKind(ProjectTypes.WEBSITE_PROJECT)
                        || project.IsKind(ProjectTypes.UNIVERSAL_APP))
                        return;

                    item.Properties.Item("ItemType").Value = "None";
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public static void AddNestedFile(string parentFile, string newFile, bool force = false)
        {
            ProjectItem item = DTE.Solution.FindProjectItem(parentFile);

            try
            {
                if (item == null
                    || item.ContainingProject == null
                    || item.ContainingProject.IsKind(ProjectTypes.ASPNET_5))
                    return;

                if (item.ProjectItems == null || item.ContainingProject.IsKind(ProjectTypes.UNIVERSAL_APP))
                {
                    item.ContainingProject.AddFileToProject(newFile);
                }
                else if (DTE.Solution.FindProjectItem(newFile) == null || force)
                {
                    item.ProjectItems.AddFromFile(newFile);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public static bool IsKind(this Project project, params string[] kindGuids)
        {
            foreach (string guid in kindGuids)
            {
                if (project.Kind.Equals(guid, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public static string GetRootFolder(this Project project)
        {
            if (project == null)
                return null;

            if (project.IsKind(ProjectKinds.vsProjectKindSolutionFolder))
                return Path.GetDirectoryName(DTE.Solution.FullName);

            if (string.IsNullOrEmpty(project.FullName))
                return null;

            string fullPath;

            try
            {
                fullPath = project.Properties.Item("FullPath").Value as string;
            }
            catch (ArgumentException)
            {
                try
                {
                    // MFC projects don't have FullPath, and there seems to be no way to query existence
                    fullPath = project.Properties.Item("ProjectDirectory").Value as string;
                }
                catch (ArgumentException)
                {
                    // Installer projects have a ProjectPath.
                    fullPath = project.Properties.Item("ProjectPath").Value as string;
                }
            }

            if (string.IsNullOrEmpty(fullPath))
                return File.Exists(project.FullName) ? Path.GetDirectoryName(project.FullName) : null;

            if (Directory.Exists(fullPath))
                return fullPath;

            if (File.Exists(fullPath))
                return Path.GetDirectoryName(fullPath);

            return null;
        }

        public static string FilePath(this ProjectItem item)
        {
            return item.FileNames[1];
        }

        public static bool IsSupportedFile(this ProjectItem item, out string FilePath)
        {
            FilePath = item.FilePath();

            if (item.Kind == EnvDTE.Constants.vsProjectItemKindPhysicalFolder)
                return false;

            string ext = Path.GetExtension(item.FilePath());
            return ext.Equals(".less", StringComparison.OrdinalIgnoreCase);
        }

        public static async Task<string> ReadFileAsync(string filePath)
        {
            using (var stream = new StreamReader(filePath))
            {
                return await stream.ReadToEndAsync();
            }

        }
    }

    public static class ProjectTypes
    {
        public const string ASPNET_5 = "{8BB2217D-0F2D-49D1-97BC-3654ED321F3B}";
        public const string DOTNET_Core = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}";
        public const string WEBSITE_PROJECT = "{E24C65DC-7377-472B-9ABA-BC803B73C61A}";
        public const string UNIVERSAL_APP = "{262852C6-CD72-467D-83FE-5EEB1973A190}";
        public const string NODE_JS = "{9092AA53-FB77-4645-B42D-1CCCA6BD08BD}";
        public const string SSDT = "{00d1a9c2-b5f0-4af3-8072-f6c62b433612}";
    }
}
