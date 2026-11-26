using System;
using UBOAT.Game.Core.Serialization;
using UBOAT.Game.Scene.Characters;
using UnityEngine.EventSystems;

namespace UInterface
{
    [NonSerializedInGameState]
    internal sealed class FirstPersonControlAxis : FirstPersonControl, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private float unitsPerPixel;
        private bool vertical;
        private int pointerId;
        private PlayableCharacter actor;

        internal void Bind(string id, IDisposable registration, float unitsPerPixel, bool vertical)
        {
            this.unitsPerPixel = unitsPerPixel;
            this.vertical = vertical;
            base.Bind(id, registration);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!actor && IsBound && eventData.button == PointerEventData.InputButton.Left &&
                CrewServicesApi.TryGetFirstPersonCrew(out var character))
            {
                pointerId = eventData.pointerId;
                actor = character;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!actor || eventData.pointerId != pointerId)
            {
                return;
            }

            if (!IsBound || !CrewServicesApi.TryGetFirstPersonCrew(out var character) || character != actor)
            {
                actor = null;
                return;
            }

            float delta = (vertical ? eventData.delta.y : eventData.delta.x) * unitsPerPixel;
            if (delta != 0f && ControlApi.TryReadAxis(ControlId, out float value))
            {
                ControlApi.TrySetAxis(ControlId, value + delta);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId)
            {
                actor = null;
            }
        }

        private void OnDisable()
        {
            actor = null;
        }

        protected override void OnClick()
        {
        }
    }
}
