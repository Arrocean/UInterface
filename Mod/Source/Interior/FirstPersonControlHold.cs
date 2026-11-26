using System;
using UBOAT.Game.Core.Serialization;
using UBOAT.Game.Scene.Characters;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UInterface
{
    [NonSerializedInGameState]
    internal sealed class FirstPersonControlHold : FirstPersonControl, IPointerDownHandler, IPointerUpHandler
    {
        private Action<PlayableCharacter, bool> changed;
        private PlayableCharacter actor;
        private int pointerId;

        internal void Bind(string id, IDisposable registration, Action<PlayableCharacter, bool> changed)
        {
            this.changed = changed;
            base.Bind(id, registration);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (ReferenceEquals(actor, null) && IsBound && eventData.button == PointerEventData.InputButton.Left &&
                ControlApi.TryGetControlMount(ControlId, out _) && CrewServicesApi.TryGetFirstPersonCrew(out var character))
            {
                actor = character;
                pointerId = eventData.pointerId;
                Notify(character, true);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId && eventData.button == PointerEventData.InputButton.Left)
            {
                Release();
            }
        }

        private void Update()
        {
            if (!ReferenceEquals(actor, null) && (!IsBound || !ControlApi.TryGetControlMount(ControlId, out _) ||
                !CrewServicesApi.TryGetFirstPersonCrew(out var character) || character != actor))
            {
                Release();
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                Release();
            }
        }

        private void OnDisable()
        {
            Release();
        }

        private void Release()
        {
            if (!ReferenceEquals(actor, null))
            {
                var character = actor;
                actor = null;
                Notify(character, false);
            }
        }

        private void Notify(PlayableCharacter character, bool held)
        {
            try
            {
                changed(character, held);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        protected override void OnClick()
        {
        }
    }
}
