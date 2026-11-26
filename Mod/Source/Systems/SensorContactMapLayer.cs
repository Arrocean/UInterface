using System.Collections.Generic;
using UBOAT.Game.Core.Serialization;
using UBOAT.Game.Scene;
using UBOAT.Game.Scene.Camera;
using UnityEngine;
using UnityEngine.UI;
using SandboxWorld = UBOAT.Game.Sandbox.Sandbox;

namespace UInterface
{
    [NonSerializedInGameState]
    internal sealed class SensorContactMapLayer : MaskableGraphic
    {
        private static readonly Color32 PositionColor = new Color32(246, 185, 94, 238);
        private static readonly Color32 BearingColor = new Color32(246, 185, 94, 170);
        private readonly List<SensorContactsApi.Contact> contacts = new List<SensorContactsApi.Contact>();
        private bool contactsChanged;
        private bool wasVisible;

        protected override void OnEnable()
        {
            base.OnEnable();
            contactsChanged = true;
            SensorContactsApi.OnContactChanged += OnContactChanged;
        }

        protected override void OnDisable()
        {
            SensorContactsApi.OnContactChanged -= OnContactChanged;
            contacts.Clear();
            wasVisible = false;
            base.OnDisable();
        }

        private void OnContactChanged(string sensorId, string contactId, bool available)
        {
            contactsChanged = true;
        }

        private void LateUpdate()
        {
            if (contactsChanged)
            {
                SensorContactsApi.CopyMapContacts(contacts);
                contactsChanged = false;
            }

            bool visible = contacts.Count != 0 && CanDraw(MainCamera.Instance);
            if (visible || wasVisible)
            {
                SetVerticesDirty();
            }

            wasVisible = visible;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var mainCamera = MainCamera.Instance;
            var camera = Camera.main;
            var sandbox = SandboxWorld.Instance;
            if (contacts.Count == 0 || !camera || !sandbox || !canvas || !CanDraw(mainCamera))
            {
                return;
            }

            var bounds = camera.pixelRect;
            var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            foreach (var contact in contacts)
            {
                if (mesh.currentVertCount > 60000)
                {
                    break;
                }

                if (contact.Position.HasValue)
                {
                    Vector2 point;
                    if (TryProject(mainCamera, sandbox.EncounterToSceneSpace(contact.Position.Value), out var screen) &&
                        bounds.Contains(screen) && ToLocal(screen, uiCamera, out point))
                    {
                        DrawDiamond(mesh, point);
                    }
                }
                else if (contact.Origin.HasValue)
                {
                    DrawBearing(mesh, mainCamera, bounds, uiCamera,
                        sandbox.EncounterToSceneSpace(contact.Origin.Value), contact.Bearing);
                }
            }
        }

        private static bool CanDraw(MainCamera camera)
        {
            if (!camera || !camera.isActiveAndEnabled || camera.CurrentMode != CameraMode.Map ||
                !SubmarineApi.TryGetReadySubmarine(out _, out _))
            {
                return false;
            }

            var mode = camera.MapController.Mode;
            return mode == MapCameraController.MapDisplayMode.Normal || mode == MapCameraController.MapDisplayMode.TacticalMap;
        }

        private void DrawBearing(VertexHelper mesh, MainCamera camera, Rect bounds, Camera uiCamera,
            Vector3 origin, float bearing)
        {
            float radians = bearing * Mathf.Deg2Rad;
            float projectionDistance = Mathf.Clamp(camera.MapController.CameraElevation * 0.1f, 1f, 1000f);
            var sample = origin + new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) * projectionDistance;
            if (!TryProject(camera, origin, out var from) || !TryProject(camera, sample, out var toward))
            {
                return;
            }

            var direction = (toward - from).normalized;
            float start = 12f;
            float end = float.PositiveInfinity;
            if (direction == Vector2.zero ||
                !ClipAxis(from.x, direction.x, bounds.xMin, bounds.xMax, ref start, ref end) ||
                !ClipAxis(from.y, direction.y, bounds.yMin, bounds.yMax, ref start, ref end) ||
                !ToLocal(from + direction * start, uiCamera, out var a) ||
                !ToLocal(from + direction * end, uiCamera, out var b))
            {
                return;
            }

            float length = Vector2.Distance(a, b);
            var localDirection = (b - a).normalized;
            float step = Mathf.Max(15f, length / 160f);
            for (float offset = 0f; offset < length; offset += step)
            {
                DrawStroke(mesh, a + localDirection * offset,
                    a + localDirection * Mathf.Min(offset + step * 0.59f, length), 1.5f, BearingColor);
            }
        }

        private static bool ClipAxis(float origin, float direction, float minimum, float maximum,
            ref float start, ref float end)
        {
            if (Mathf.Abs(direction) < 0.0001f)
            {
                return origin >= minimum && origin <= maximum;
            }

            float near = (minimum - origin) / direction;
            float far = (maximum - origin) / direction;
            start = Mathf.Max(start, Mathf.Min(near, far));
            end = Mathf.Min(end, Mathf.Max(near, far));
            return end > start;
        }

        private static bool TryProject(MainCamera camera, Vector3 world, out Vector2 screen)
        {
            var point = camera.WorldToScreenPointNonJitteredExternalView(world);
            screen = new Vector2(point.x, point.y);
            return point.z > 0f && !float.IsNaN(point.x) && !float.IsNaN(point.y) &&
                !float.IsInfinity(point.x) && !float.IsInfinity(point.y);
        }

        private bool ToLocal(Vector2 screen, Camera uiCamera, out Vector2 point)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, uiCamera, out point);
        }

        private static void DrawDiamond(VertexHelper mesh, Vector2 center)
        {
            const float radius = 7f;
            var top = center + Vector2.up * radius;
            var bottom = center + Vector2.down * radius;
            var left = center + Vector2.left * radius;
            var right = center + Vector2.right * radius;
            DrawStroke(mesh, top, right, 2f, PositionColor);
            DrawStroke(mesh, right, bottom, 2f, PositionColor);
            DrawStroke(mesh, bottom, left, 2f, PositionColor);
            DrawStroke(mesh, left, top, 2f, PositionColor);
        }

        private static void DrawStroke(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color32 color)
        {
            var delta = b - a;
            if (delta.sqrMagnitude < 0.001f)
            {
                return;
            }

            var normal = new Vector2(-delta.y, delta.x).normalized * (width * 0.5f);
            int index = mesh.currentVertCount;
            mesh.AddVert(a - normal, color, Vector2.zero);
            mesh.AddVert(a + normal, color, Vector2.zero);
            mesh.AddVert(b + normal, color, Vector2.zero);
            mesh.AddVert(b - normal, color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
