using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy의 동작을 상태머신으로 관리합니다.
/// - 대기: 플레이어가 4칸 이내면 추적으로 전환
/// - 추적: 플레이어가 5칸 이상이면 대기로 전환
/// </summary>
public class Enemy : MonoBehaviour
{
    public enum EnemyVisualStateId
    {
        Wait,
        Chase
    }

    [Header("Visual")]
    [SerializeField] private EnemyVisualColorStateApplier visualColorStateApplier;

    [Header("References")]
    [SerializeField] private EnemyControllerBase enemyController;
    [SerializeField] private CellController cellController;
    [SerializeField] private Transform playerTransform;

    [Header("State Switch Thresholds")]
    [Tooltip("플레이어가 이 거리(칸) 이내면 대기->추적으로 전환")]
    [SerializeField] private int chaseEnterDistance = 4;

    [Tooltip("플레이어가 이 거리(칸) 이상이면 추적->대기로 전환")]
    [SerializeField] private int chaseExitDistance = 5;

    [Tooltip("상태 전환 판정 주기(초). 너무 짧으면 불필요하게 BFS를 반복합니다.")]
    [SerializeField] private float decisionIntervalSeconds = 0.2f;

    private int _nextDecisionFrame;
    private IEnemyState _currentState;

    private interface IEnemyState
    {
        void Enter();
        void Tick();
        void Exit();
    }

    private sealed class WaitState : IEnemyState
    {
        private readonly Enemy _enemy;

        public WaitState(Enemy enemy)
        {
            _enemy = enemy;
        }

        public void Enter()
        {
            _enemy.enemyController?.SetRuntimeAIEnabled(false);
            _enemy.visualColorStateApplier?.ApplyState(EnemyVisualStateId.Wait);
        }

        public void Tick()
        {
            if (_enemy.IsPlayerWithinSteps(_enemy.chaseEnterDistance))
                _enemy.SwitchState(_enemy._chaseState);
        }

        public void Exit()
        {
        }
    }

    private sealed class ChaseState : IEnemyState
    {
        private readonly Enemy _enemy;

        public ChaseState(Enemy enemy)
        {
            _enemy = enemy;
        }

        public void Enter()
        {
            _enemy.enemyController?.SetRuntimeAIEnabled(true);
            _enemy.visualColorStateApplier?.ApplyState(EnemyVisualStateId.Chase);
        }

        public void Tick()
        {
            // requirement: 플레이어가 5칸 이상이면 대기로 전환
            if (!_enemy.IsPlayerWithinSteps(_enemy.chaseExitDistance - 1))
                _enemy.SwitchState(_enemy._waitState);
        }

        public void Exit()
        {
        }
    }

    private WaitState _waitState;
    private ChaseState _chaseState;

    private void Awake()
    {
        if (enemyController == null)
            enemyController = GetComponent<EnemyControllerBase>();

        if (cellController == null)
            cellController = FindFirstObjectByType<CellController>();

        if (playerTransform == null)
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null)
                playerTransform = player.transform;
        }

        // 인스펙터 미설정 시, 자식 컴포넌트를 자동 탐색합니다.
        if (visualColorStateApplier == null)
            visualColorStateApplier = GetComponentInChildren<EnemyVisualColorStateApplier>(true);
    }

    private void Start()
    {
        _waitState = new WaitState(this);
        _chaseState = new ChaseState(this);

        SwitchState(_waitState);
        _nextDecisionFrame = 0;
    }

    private void Update()
    {
        // 결정 주기 기반으로 BFS 반복을 줄입니다.
        if (Time.frameCount < _nextDecisionFrame)
            return;

        float safeDt = Mathf.Max(Time.deltaTime, 0.0001f);
        int frames = Mathf.CeilToInt(decisionIntervalSeconds / safeDt);
        _nextDecisionFrame = Time.frameCount + Mathf.Max(frames, 1);

        _currentState?.Tick();
    }

    private void SwitchState(IEnemyState nextState)
    {
        if (nextState == null || nextState == _currentState)
            return;

        _currentState?.Exit();
        _currentState = nextState;
        _currentState.Enter();
    }

    /// <summary>
    /// 적(현재 Enemy 위치)에서 플레이어 위치까지의 “그리드 최단거리(칸)”가 maxSteps 이내인지 확인합니다.
    /// - availability(IsAvailable) 미반영: 상태 전환은 접근 거리 기준만 사용
    /// - Cell adjacency(BFS)만 사용
    /// </summary>
    private bool IsPlayerWithinSteps(int maxSteps)
    {
        if (cellController == null || playerTransform == null)
            return false;

        if (!cellController.TryGetCellAtWorldPosition(transform.position, out Cell enemyCell))
            return false;

        if (!cellController.TryGetCellAtWorldPosition(playerTransform.position, out Cell playerCell))
            return false;

        if (enemyCell == playerCell)
            return true;

        var visited = new HashSet<Cell>();
        var queue = new Queue<(Cell cell, int depth)>();

        visited.Add(enemyCell);
        queue.Enqueue((enemyCell, 0));

        Vector3[] dirs =
        {
            Vector3.forward,
            Vector3.back,
            Vector3.right,
            Vector3.left
        };

        while (queue.Count > 0)
        {
            var item = queue.Dequeue();
            Cell current = item.cell;
            int depth = item.depth;

            if (depth >= maxSteps)
                continue;

            for (int i = 0; i < dirs.Length; i++)
            {
                Vector3 dir = dirs[i];
                Cell neighbor = cellController.GetNeighborCell(current, dir);
                if (neighbor == null || visited.Contains(neighbor))
                    continue;

                if (neighbor == playerCell)
                    return true;

                visited.Add(neighbor);
                queue.Enqueue((neighbor, depth + 1));
            }
        }

        return false;
    }
}
