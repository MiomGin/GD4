using System.Collections.Generic;
using UnityEngine;

namespace Dungeon.EnemySystem
{
    /// <summary>
    /// 负责让敌人沿一组世界坐标路径点移动。
    /// 本脚本不负责寻路，只负责执行其他系统提供的路径。
    /// </summary>
    public sealed class EnemyMovement : MonoBehaviour
    {
        [Header("移动设置")]

        [SerializeField, Min(0f)]
        private float moveSpeed = 2f;

        [SerializeField, Min(0.001f)]
        private float arriveDistance = 0.05f;

        [SerializeField]
        private Rigidbody2D body;

        [Header("测试路径")]

        [Tooltip("用于第一阶段直接测试移动的路径点。")]
        [SerializeField]
        private Transform[] initialWaypoints;

        [SerializeField]
        private bool loopInitialWaypoints;

        private readonly List<Vector3> path =
            new List<Vector3>();

        private int currentPathIndex;
        private bool loopPath;

        /// <summary>
        /// 当前是否存在尚未走完的路径。
        /// </summary>
        public bool IsMoving =>
            path.Count > 0 &&
            currentPathIndex < path.Count;

        private void Awake()
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }
        }

        private void Start()
        {
            LoadInitialWaypoints();
        }

        private void FixedUpdate()
        {
            MoveAlongPath();
        }

        /// <summary>
        /// 设置新的世界坐标移动路径。
        /// 设置后会替换当前尚未完成的路径。
        /// </summary>
        /// <param name="worldPath">
        /// 按移动顺序排列的世界坐标。
        /// </param>
        /// <param name="loop">
        /// 到达终点后是否重新从第一个路径点开始。
        /// </param>
        public void SetPath(
            IEnumerable<Vector3> worldPath,
            bool loop = false)
        {
            path.Clear();
            currentPathIndex = 0;
            loopPath = loop;

            if (worldPath == null)
            {
                return;
            }

            foreach (Vector3 point in worldPath)
            {
                path.Add(point);
            }
        }

        /// <summary>
        /// 设置一个单独的目标位置。
        /// </summary>
        public void SetDestination(Vector3 worldPosition)
        {
            path.Clear();
            path.Add(worldPosition);

            currentPathIndex = 0;
            loopPath = false;
        }

        /// <summary>
        /// 清除当前移动路径并停止移动。
        /// </summary>
        public void Stop()
        {
            path.Clear();
            currentPathIndex = 0;

            if (body != null)
            {
                body.velocity = Vector2.zero;
            }
        }

        private void MoveAlongPath()
        {
            if (!IsMoving)
            {
                return;
            }

            Vector2 currentPosition =
                body != null
                    ? body.position
                    : transform.position;

            Vector2 targetPosition =
                path[currentPathIndex];

            Vector2 nextPosition =
                Vector2.MoveTowards(
                    currentPosition,
                    targetPosition,
                    moveSpeed * Time.fixedDeltaTime
                );

            if (body != null)
            {
                body.MovePosition(nextPosition);
            }
            else
            {
                transform.position =
                    new Vector3(
                        nextPosition.x,
                        nextPosition.y,
                        transform.position.z
                    );
            }

            if (Vector2.Distance(
                    nextPosition,
                    targetPosition) >
                arriveDistance)
            {
                return;
            }

            AdvancePath();
        }

        private void AdvancePath()
        {
            currentPathIndex++;

            if (currentPathIndex < path.Count)
            {
                return;
            }

            if (loopPath && path.Count > 0)
            {
                currentPathIndex = 0;
                return;
            }

            path.Clear();
            currentPathIndex = 0;
        }

        private void LoadInitialWaypoints()
        {
            if (initialWaypoints == null ||
                initialWaypoints.Length == 0)
            {
                return;
            }

            List<Vector3> positions =
                new List<Vector3>();

            foreach (Transform waypoint
                     in initialWaypoints)
            {
                if (waypoint != null)
                {
                    positions.Add(waypoint.position);
                }
            }

            SetPath(
                positions,
                loopInitialWaypoints
            );
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0f, moveSpeed);
            arriveDistance =
                Mathf.Max(0.001f, arriveDistance);
        }
#endif
    }
}