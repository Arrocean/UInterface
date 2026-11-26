using System;
using UBOAT.Game.Core.Serialization;
using UBOAT.Game.Scene.Items;
using UnityEngine;

namespace UInterface
{
    [NonSerializedInGameState]
    internal abstract class FirstPersonControl : FPPInteractionObject, IDisposable
    {
        private string controlId;
        private IDisposable registration;

        protected string ControlId => controlId;
        protected bool IsBound => registration != null && isActiveAndEnabled;

        internal void Bind(string id, IDisposable controlRegistration)
        {
            controlId = id;
            registration = controlRegistration;
            PlayerSubmarineContext.Invalidated += Dispose;
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

        protected void OnDestroy()
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

    [NonSerializedInGameState]
    internal sealed class FirstPersonControlButton : FirstPersonControl
    {
        protected override void OnClick()
        {
            if (IsBound)
            {
                ControlApi.TryPress(ControlId);
            }
        }
    }
}
