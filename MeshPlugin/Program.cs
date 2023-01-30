using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UABEAvalonia;
using UABEAvalonia.Plugins;

namespace MeshPlugin
{
    public class ExportMeshOption : UABEAPluginOption
    {
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetContainer> selection, out string name)
        {
            name = "Export .obj";

            if (action != UABEAPluginAction.Export)
                return false;

            int classId = AssetHelper.FindAssetClassByName(am.classDatabase, "Mesh").ClassId;

            foreach (AssetContainer cont in selection)
            {
                if (cont.ClassId != classId)
                    return false;
            }
            return true;
        }

        public async Task<bool> ExecutePlugin(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            if (selection.Count > 1)
                return await BatchExport(win, workspace, selection);
            else
                return await SingleExport(win, workspace, selection);
        }

        public async Task<bool> BatchExport(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            OpenFolderDialog ofd = new OpenFolderDialog();
            ofd.Title = "Select export directory";

            string dir = await ofd.ShowAsync(win);

            if (dir != null && dir != string.Empty)
            {
                foreach (AssetContainer cont in selection)
                {
                    AssetTypeValueField baseField = workspace.GetBaseField(cont);

                    MeshClass.Mesh mesh = new MeshClass.Mesh(baseField, cont.FileInstance);
                    {
                        if (mesh.m_VertexData.m_VertexCount <= 0)
                            return false;
                        var sb = new StringBuilder();
                        sb.AppendLine("g " + mesh.m_Name);
                        #region Vertices
                        if (mesh.m_Vertices == null || mesh.m_Vertices.Length == 0)
                        {
                            return false;
                        }
                        int c = 3;
                        if (mesh.m_Vertices.Length == mesh.m_VertexData.m_VertexCount * 4)
                        {
                            c = 4;
                        }
                        for (int v = 0; v < mesh.m_VertexData.m_VertexCount; v++)
                        {
                            sb.AppendFormat("v {0} {1} {2}\r\n", -mesh.m_Vertices[v * c], mesh.m_Vertices[v * c + 1], mesh.m_Vertices[v * c + 2]);
                        }
                        #endregion

                        #region UV
                        if (mesh.m_UV0?.Length > 0)
                        {
                            if (mesh.m_UV0.Length == mesh.m_VertexData.m_VertexCount * 2)
                            {
                                c = 2;
                            }
                            else if (mesh.m_UV0.Length == mesh.m_VertexData.m_VertexCount * 3)
                            {
                                c = 3;
                            }
                            for (int v = 0; v < mesh.m_VertexData.m_VertexCount; v++)
                            {
                                sb.AppendFormat("vt {0} {1}\r\n", mesh.m_UV0[v * c], mesh.m_UV0[v * c + 1]);
                            }
                        }
                        #endregion

                        #region Normals
                        if (mesh.m_Normals?.Length > 0)
                        {
                            if (mesh.m_Normals.Length == mesh.m_VertexData.m_VertexCount * 3)
                            {
                                c = 3;
                            }
                            else if (mesh.m_Normals.Length == mesh.m_VertexData.m_VertexCount * 4)
                            {
                                c = 4;
                            }
                            for (int v = 0; v < mesh.m_VertexData.m_VertexCount; v++)
                            {
                                sb.AppendFormat("vn {0} {1} {2}\r\n", -mesh.m_Normals[v * c], mesh.m_Normals[v * c + 1], mesh.m_Normals[v * c + 2]);
                            }
                        }
                        #endregion

                        #region Face
                        int sum = 0;
                        for (var i = 0; i < mesh.m_SubMeshes.Length; i++)
                        {
                            sb.AppendLine($"g {mesh.m_Name}_{i}");
                            int indexCount = (int)mesh.m_SubMeshes[i].indexCount;
                            var end = sum + indexCount / 3;
                            for (int f = sum; f < end; f++)
                            {
                                sb.AppendFormat("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\r\n", mesh.m_Indices[f * 3 + 2] + 1, mesh.m_Indices[f * 3 + 1] + 1, mesh.m_Indices[f * 3] + 1);
                            }
                            sum = end;
                        }
                        #endregion

                        sb.Replace("NaN", "0");

                        string file = Path.Combine(dir, $"{mesh.m_Name}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}.obj");

                        File.WriteAllBytes(file, Encoding.ASCII.GetBytes(sb.ToString()));
                    }
                }
                return true;
            }
            return false;
        }
        public async Task<bool> SingleExport(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            AssetContainer cont = selection[0];

            SaveFileDialog sfd = new SaveFileDialog();

            AssetTypeValueField baseField = workspace.GetBaseField(cont);

            MeshClass.Mesh mesh = new MeshClass.Mesh(baseField, cont.FileInstance);
            {
                if (mesh.m_VertexData.m_VertexCount <= 0)
                    return false;
                var sb = new StringBuilder();
                sb.AppendLine("g " + mesh.m_Name);
                #region Vertices
                if (mesh.m_Vertices == null || mesh.m_Vertices.Length == 0)
                {
                    return false;
                }
                int c = 3;
                if (mesh.m_Vertices.Length == mesh.m_VertexData.m_VertexCount * 4)
                {
                    c = 4;
                }
                for (int v = 0; v < mesh.m_VertexData.m_VertexCount; v++)
                {
                    sb.AppendFormat("v {0} {1} {2}\r\n", -mesh.m_Vertices[v * c], mesh.m_Vertices[v * c + 1], mesh.m_Vertices[v * c + 2]);
                }
                #endregion

                #region UV
                if (mesh.m_UV0?.Length > 0)
                {
                    if (mesh.m_UV0.Length == mesh.m_VertexData.m_VertexCount * 2)
                    {
                        c = 2;
                    }
                    else if (mesh.m_UV0.Length == mesh.m_VertexData.m_VertexCount * 3)
                    {
                        c = 3;
                    }
                    for (int v = 0; v < mesh.m_VertexData.m_VertexCount; v++)
                    {
                        sb.AppendFormat("vt {0} {1}\r\n", mesh.m_UV0[v * c], mesh.m_UV0[v * c + 1]);
                    }
                }
                #endregion

                #region Normals
                if (mesh.m_Normals?.Length > 0)
                {
                    if (mesh.m_Normals.Length == mesh.m_VertexData.m_VertexCount * 3)
                    {
                        c = 3;
                    }
                    else if (mesh.m_Normals.Length == mesh.m_VertexData.m_VertexCount * 4)
                    {
                        c = 4;
                    }
                    for (int v = 0; v < mesh.m_VertexData.m_VertexCount; v++)
                    {
                        sb.AppendFormat("vn {0} {1} {2}\r\n", -mesh.m_Normals[v * c], mesh.m_Normals[v * c + 1], mesh.m_Normals[v * c + 2]);
                    }
                }
                #endregion

                #region Face
                int sum = 0;
                for (var i = 0; i < mesh.m_SubMeshes.Length; i++)
                {
                    sb.AppendLine($"g {mesh.m_Name}_{i}");
                    int indexCount = (int)mesh.m_SubMeshes[i].indexCount;
                    var end = sum + indexCount / 3;
                    for (int f = sum; f < end; f++)
                    {
                        sb.AppendFormat("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\r\n", mesh.m_Indices[f * 3 + 2] + 1, mesh.m_Indices[f * 3 + 1] + 1, mesh.m_Indices[f * 3] + 1);
                    }
                    sum = end;
                }
                #endregion

                sb.Replace("NaN", "0");

                sfd.Title = "Save mesh file";
                sfd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "Mesh file", Extensions = new List<string>() { "obj" } }
            };
                sfd.InitialFileName = $"{mesh.m_Name}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}.obj";

                string file = await sfd.ShowAsync(win);

                if (file != null && file != string.Empty)
                {
                    File.WriteAllBytes(file, Encoding.ASCII.GetBytes(sb.ToString()));

                    return true;
                }
                return false;
            }
        }
    }

    public class TextAssetPlugin : UABEAPlugin
    {
        public PluginInfo Init()
        {
            PluginInfo info = new PluginInfo();
            info.name = "Mesh Export";

            info.options = new List<UABEAPluginOption>();
            info.options.Add(new ExportMeshOption());
            return info;
        }
    }
}
