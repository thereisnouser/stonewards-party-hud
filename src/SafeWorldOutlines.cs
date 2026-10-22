using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StonewardsPartyHUD
{
    // Non-invasive world-space outline pass. In contrast to QuickOutline,
    // this class never inserts new materials into player Renderer.materials.
    // It draws the game's existing mask/fill shaders through a camera buffer.
    internal sealed class SafeWorldOutlines : IDisposable
    {
        private const CameraEvent OutlineEvent = CameraEvent.AfterForwardAlpha;
        private Camera _camera;
        private CommandBuffer _commands;
        private Material _mask;
        private Material _fill;
        private Material _downedFill;
        private float _retryAt;
        private bool _reportedReady;
        private int _lastDrawableCount = -1;

        private static bool Usable(Material value)
        {
            return value != null && value.shader != null &&
                value.shader.isSupported &&
                value.shader.name.IndexOf("InternalErrorShader",
                    StringComparison.OrdinalIgnoreCase) < 0;
        }

        private bool LoadMaterials(Action<string> warning)
        {
            if (Usable(_mask) && Usable(_fill) && Usable(_downedFill))
                return true;
            if (Time.unscaledTime < _retryAt)
                return false;

            ReleaseMaterials();
            var sourceMask = Resources.Load<Material>("Materials/OutlineMask");
            var sourceFill = Resources.Load<Material>("Materials/OutlineFill");
            if (!Usable(sourceMask) || !Usable(sourceFill))
            {
                _retryAt = Time.unscaledTime + 20f;
                warning("Safe world outlines: Stonewards outline mask/fill materials " +
                    "were not found or their shaders are unsupported; " +
                    "no character materials were changed.");
                return false;
            }

            try
            {
                _mask = UnityEngine.Object.Instantiate(sourceMask);
                _fill = UnityEngine.Object.Instantiate(sourceFill);
                _downedFill = UnityEngine.Object.Instantiate(sourceFill);
                _mask.name = "PartyHUD Safe Outline Mask";
                _fill.name = "PartyHUD Safe Outline Fill";
                _downedFill.name = "PartyHUD Safe Outline Downed";
                _mask.hideFlags = HideFlags.HideAndDontSave;
                _fill.hideFlags = HideFlags.HideAndDontSave;
                _downedFill.hideFlags = HideFlags.HideAndDontSave;
                _reportedReady = false;
                return true;
            }
            catch (Exception ex)
            {
                ReleaseMaterials();
                _retryAt = Time.unscaledTime + 20f;
                warning("Safe world outline material initialization failed: " + ex);
                return false;
            }
        }

        private void ReleaseMaterials()
        {
            if (_mask != null)
                UnityEngine.Object.Destroy(_mask);
            if (_fill != null)
                UnityEngine.Object.Destroy(_fill);
            if (_downedFill != null)
                UnityEngine.Object.Destroy(_downedFill);
            _mask = null;
            _fill = null;
            _downedFill = null;
        }

        private void DetachCamera()
        {
            if (_commands == null)
                return;
            if (_camera != null)
                _camera.RemoveCommandBuffer(OutlineEvent, _commands);
            _commands.Clear();
            _commands.Release();
            _commands = null;
            _camera = null;
            _lastDrawableCount = -1;
        }

        public void Clear()
        {
            DetachCamera();
        }

        public void Dispose()
        {
            DetachCamera();
            ReleaseMaterials();
        }

        private bool AttachCamera(Camera camera, Action<string> info)
        {
            if (camera == _camera && _commands != null)
                return true;

            DetachCamera();
            if (camera == null)
                return false;

            _commands = new CommandBuffer { name = "PartyHUD Safe Teammate Outlines" };
            camera.AddCommandBuffer(OutlineEvent, _commands);
            _camera = camera;
            info("Safe teammate outlines attached to camera '" +
                camera.name + "' (non-invasive command buffer).");
            return true;
        }

        private static int SubmeshCount(Renderer renderer)
        {
            var skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null)
                return skinned.sharedMesh != null
                    ? skinned.sharedMesh.subMeshCount : 0;

            var meshRenderer = renderer as MeshRenderer;
            if (meshRenderer == null)
                return 0;
            var filter = meshRenderer.GetComponent<MeshFilter>();
            return filter != null && filter.sharedMesh != null
                ? filter.sharedMesh.subMeshCount : 0;
        }

        public void Refresh(IEnumerable<FirstPersonController> roster,
            Func<FirstPersonController, GameObject> visuals,
            bool throughWalls, float width,
            Action<string> info, Action<string> warning)
        {
            if (roster == null)
            {
                Clear();
                return;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                Clear();
                return;
            }

            if (!LoadMaterials(warning))
            {
                Clear();
                return;
            }

            AttachCamera(camera, info);
            _commands.Clear();

            _mask.SetFloat("_ZTest", (float)CompareFunction.Always);
            float fillZ = (float)(throughWalls
                ? CompareFunction.Always : CompareFunction.LessEqual);
            _fill.SetFloat("_ZTest", fillZ);
            _downedFill.SetFloat("_ZTest", fillZ);
            _fill.SetFloat("_OutlineWidth", width);
            _downedFill.SetFloat("_OutlineWidth", width);
            _fill.SetColor("_OutlineColor", new Color(0.24f, 0.88f, 0.96f, 1f));
            _downedFill.SetColor("_OutlineColor", new Color(1f, 0.34f, 0.36f, 1f));

            int drawn = 0;
            foreach (var player in roster)
            {
                if (player == null || player.isLocalPlayer)
                    continue;

                var root = visuals(player);
                if (root == null || !root.activeInHierarchy)
                    continue;

                bool downed = player.PlayerStats != null &&
                    player.PlayerStats.NetworkisDead;
                var fill = downed ? _downedFill : _fill;

                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    if (renderer == null || !renderer.enabled ||
                        !renderer.gameObject.activeInHierarchy ||
                        (!(renderer is MeshRenderer) &&
                         !(renderer is SkinnedMeshRenderer)))
                        continue;

                    var materials = renderer.sharedMaterials;
                    if (materials == null || materials.Length == 0)
                        continue;

                    bool safe = true;
                    foreach (var material in materials)
                        if (!Usable(material))
                        {
                            safe = false;
                            break;
                        }
                    if (!safe)
                        continue;

                    // DrawRenderer uses the actual animated renderer, but does
                    // not touch the material array or its current skin.
                    // Render every mesh submesh, including character outfits.
                    int parts = SubmeshCount(renderer);
                    for (int part = 0; part < parts; part++)
                    {
                        _commands.DrawRenderer(renderer, _mask, part);
                        _commands.DrawRenderer(renderer, fill, part);
                        drawn++;
                    }
                }
            }

            if (!_reportedReady || drawn != _lastDrawableCount)
            {
                info("Safe teammate outline render passes: " + drawn +
                    " (camera=" + camera.name + ", throughWalls=" +
                    throughWalls + ").");
                _reportedReady = true;
                _lastDrawableCount = drawn;
            }
        }
    }
}
