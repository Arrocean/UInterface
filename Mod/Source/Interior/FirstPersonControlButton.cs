using System;
using UBOAT.Game.Core.Serialization;
using UBOAT.Game.Scene.Items;
using UnityEngine;

namespace UInterface
{
    [NonSerializedInGameState]
    internal sealed class FirstPersonControlButton : FPPInteractionObject, IDisposable
    {
        private string controlId;
        private IDisposable registration;

        internal void Bind(string id, IDisposable controlRegistration)
        {
            controlId = id;
            registration = controlRegistration;
            PlayerSubmarineContext.Invalidated += Dispose;
        }

        protected override void OnClick()
        {
            if (registration != null && enabled)
            {
                ControlApi.TryPress(controlId);
            }
        }

        protected override string GetTooltipContents()
        {
            return null;
        }

        public void Dispose()
        {
            if (Unbind() && this)
            {
                enabled = false;
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private bool Unbind()
        {
            if (registration == null)
            {
                return false;
            }

            PlayerSubmarineContext.Invalidated -= Dispose;
            var previous = registration;
            registration = null;
            previous.Dispose();
            return true;
        }
    }
}
