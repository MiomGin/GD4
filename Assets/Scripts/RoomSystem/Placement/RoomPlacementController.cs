using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Dungeon.RoomSystem
{
    /// <summary>
    /// 处理建造状态、鼠标跟随、旋转、确认和取消放置。
    /// </summary>
    public sealed class RoomPlacementController :
        MonoBehaviour
    {
        [Header("引用")]

        [SerializeField]
        private DungeonGrid dungeonGrid;

        [SerializeField]
        private RoomPlacementPreview placementPreview;

        [SerializeField]
        private Camera worldCamera;

        [Header("放置设置")]

        [Tooltip("放置成功后是否继续持有相同房间进行建造。")]
        [SerializeField]
        private bool continuePlacementAfterPlace;

        private RoomData currentRoomData;
        private Vector2Int currentAnchorCell;

        private int currentRotation;
        private int beginPlacementFrame;

        private bool currentPlacementValid;

        /// <summary>
        /// 当前是否处于房间建造状态。
        /// </summary>
        public bool IsPlacingRoom =>
            currentRoomData != null;

        private void Awake()
        {
            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (!IsPlacingRoom)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape) ||
                Input.GetMouseButtonDown(1))
            {
                CancelPlacement();
                return;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                RotateClockwise();
            }

            bool pointerOverUI =
                EventSystem.current != null &&
                EventSystem.current
                    .IsPointerOverGameObject();

            if (pointerOverUI)
            {
                placementPreview?.Hide();
                return;
            }

            RefreshPreview();

            bool canConfirm =
                Time.frameCount >
                beginPlacementFrame;

            if (canConfirm &&
                Input.GetMouseButtonDown(0))
            {
                ConfirmPlacement();
            }
        }

        /// <summary>
        /// 开始建造指定房间。
        /// 可直接绑定到 UI Button 的 OnClick。
        /// </summary>
        public void BeginPlacement(
            RoomData roomData)
        {
            if (roomData == null)
            {
                return;
            }

            currentRoomData = roomData;
            currentRotation = 0;
            beginPlacementFrame = Time.frameCount;

            RefreshPreview();
        }

        /// <summary>
        /// 取消当前建造状态。
        /// </summary>
        public void CancelPlacement()
        {
            currentRoomData = null;
            currentRotation = 0;
            currentPlacementValid = false;

            placementPreview?.Hide();
        }

        /// <summary>
        /// 将当前房间顺时针旋转 90°。
        /// </summary>
        public void RotateClockwise()
        {
            if (!IsPlacingRoom)
            {
                return;
            }

            currentRotation =
                (currentRotation + 1) % 4;

            RefreshPreview();
        }

        /// <summary>
        /// 尝试在当前鼠标位置确认放置房间。
        /// </summary>
        public void ConfirmPlacement()
        {
            if (!IsPlacingRoom ||
                !currentPlacementValid)
            {
                return;
            }

            bool placed =
                dungeonGrid.TryPlaceRoom(
                    currentRoomData,
                    currentAnchorCell,
                    currentRotation,
                    out _
                );

            if (!placed)
            {
                return;
            }

            if (continuePlacementAfterPlace)
            {
                RefreshPreview();
            }
            else
            {
                CancelPlacement();
            }
        }

        private void RefreshPreview()
        {
            if (!IsPlacingRoom ||
                dungeonGrid == null ||
                placementPreview == null)
            {
                return;
            }

            if (!TryGetPointerWorldPosition(
                    out Vector3 pointerWorld))
            {
                placementPreview.Hide();
                return;
            }

            currentAnchorCell =
                dungeonGrid.WorldToCell(
                    pointerWorld
                );

            currentPlacementValid =
                dungeonGrid.CanPlace(
                    currentRoomData,
                    currentAnchorCell,
                    currentRotation,
                    out List<Vector2Int> targetCells
                );

            placementPreview.Show(
                dungeonGrid,
                targetCells,
                currentPlacementValid
            );
        }

        private bool TryGetPointerWorldPosition(
            out Vector3 worldPosition)
        {
            worldPosition = default;

            if (worldCamera == null ||
                dungeonGrid == null)
            {
                return false;
            }

            Ray ray =
                worldCamera.ScreenPointToRay(
                    Input.mousePosition
                );

            Plane gridPlane =
                new Plane(
                    dungeonGrid.transform.forward,
                    dungeonGrid.transform.position
                );

            if (!gridPlane.Raycast(
                    ray,
                    out float enter))
            {
                return false;
            }

            worldPosition = ray.GetPoint(enter);
            return true;
        }
    }
}