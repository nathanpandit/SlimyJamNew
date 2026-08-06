using SlimyJam.Core;
using SlimyJam.Gameplay;
using SlimyJam.Level;
using UnityEngine;

namespace SlimyJam.InputSystem
{
    /// <summary>
    /// Pointer lifecycle, endpoint selection, tek aktif rope kontrolü ve release (GDD 6, 14.1).
    /// Multi-touch ile ikinci rope hareketi başlatılamaz (GDD 6.2).
    /// </summary>
    public sealed class SlimyInputController : MonoBehaviour, IEndpointCandidateSource
    {
        [SerializeField] private SlimyGameManager gameManager;
        [SerializeField] private SlimyCameraController cameraController;

        private Rope _selectedRope;
        private int _activeFingerId = -1;

        public Rope SelectedRope => _selectedRope;

        private void Awake()
        {
            if (gameManager == null) gameManager = FindFirstObjectByType<SlimyGameManager>();
            if (cameraController == null) cameraController = FindFirstObjectByType<SlimyCameraController>();
        }

        private void Update()
        {
            if (gameManager == null || gameManager.State != SlimyGameState.Playing)
            {
                if (_selectedRope != null) ReleaseSelection();
                return;
            }

            if (UnityEngine.Input.touchCount > 0)
            {
                HandleTouch();
                return;
            }

            HandleMouse();
        }

        private void HandleTouch()
        {
            for (int i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                var touch = UnityEngine.Input.GetTouch(i);

                if (touch.phase == TouchPhase.Began && _selectedRope == null)
                {
                    if (!TrySelect(touch.position)) continue;
                    _activeFingerId = touch.fingerId;
                    continue;
                }

                if (touch.fingerId != _activeFingerId || _selectedRope == null) continue;

                switch (touch.phase)
                {
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        Drag(touch.position);
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        ReleaseSelection();
                        break;
                }
            }
        }

        private void HandleMouse()
        {
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                TrySelect(UnityEngine.Input.mousePosition);
                return;
            }

            if (UnityEngine.Input.GetMouseButton(0) && _selectedRope != null)
            {
                Drag(UnityEngine.Input.mousePosition);
                return;
            }

            if (UnityEngine.Input.GetMouseButtonUp(0) && _selectedRope != null)
            {
                ReleaseSelection();
            }
        }

        /// <summary>
        /// Yalnızca head ve tail aday değerlendirilir; body segmentleri seçim mesafesine dahil edilmez (GDD 6.1).
        /// Eşit mesafede rope listesi sırası, aynı rope içinde head -> tail önceliği geçerlidir.
        /// </summary>
        private bool TrySelect(Vector2 screenPosition)
        {
            if (!cameraController.TryGetWorldPosition(screenPosition, out var pointerWorld)) return false;

            var selectionRadius = gameManager.Context.Config.selectionRadius;
            if (!EndpointSelector.TrySelect(this, pointerWorld, selectionRadius, out var index, out var end))
            {
                return false;
            }

            _selectedRope = gameManager.Ropes[index];
            _selectedRope.BeginDrag(end);
            Drag(screenPosition);
            return true;
        }

        int IEndpointCandidateSource.Count => gameManager.Ropes.Count;

        bool IEndpointCandidateSource.IsSelectable(int index)
        {
            var rope = gameManager.Ropes[index];
            return rope != null && rope.IsSelectable;
        }

        Vector3 IEndpointCandidateSource.GetEndpointPosition(int index, RopeEnd end)
        {
            return gameManager.Ropes[index].GetEndpointPosition(end);
        }

        private void Drag(Vector2 screenPosition)
        {
            if (_selectedRope == null) return;
            if (!cameraController.TryGetWorldPosition(screenPosition, out var pointerWorld)) return;

            _selectedRope.Drag(pointerWorld, Time.deltaTime);

            // Collection sırasında rope kontrolü hemen kesilir (GDD 10.2).
            if (_selectedRope.State != RopeState.Dragging) ClearSelection();
        }

        private void ReleaseSelection()
        {
            if (_selectedRope != null) _selectedRope.Release();
            ClearSelection();
        }

        private void ClearSelection()
        {
            _selectedRope = null;
            _activeFingerId = -1;
        }
    }
}
