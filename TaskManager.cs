using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Addendum to krockot's original task manager
/// Add possibility to pool a bunch of tasks together, so they can be started, stopped,... together
/// </summary>
public class TaskPool {
    public delegate void AllFinishedHandler();

    private readonly List<Task> tasks = new();
    private int finishedTaskCounter;

    public bool AllRunning => CheckIfAllAreRunning();
    public bool AllPaused => CheckIfAllArePaused();
    private bool PoolLocked { get; set; }
    private bool finished = false;

    /// <summary>
    /// Gets triggered when all tasks finished.
    /// </summary>
    public event AllFinishedHandler AllFinished;

    /// <summary>
    ///     Add a task to the pool
    /// </summary>
    /// <param name="_task">The task, that should be added to the pool</param>
    /// <returns>
    ///     Indicates if a task could be added successfully to the pool.
    ///     If pool is already running, it gets locked and no further task can be added.
    /// </returns>
    public bool Add(PoolTask _task) {
        if (PoolLocked) {
            return false;
        }

        tasks.Add(_task);
        _task.Finished += ATaskFinished;
        return true;
    }

    /// <summary>
    /// Start all tasks within the pool together
    /// </summary>
    public void StartAll() {
        PoolLocked = true;
        foreach (Task task in tasks) {
            task.Start();
        }
    }

    /// <summary>
    /// Stop all the tasks within the pool together
    /// </summary>
    public void StopAll() {
        foreach (Task task in tasks) {
            task.Stop();
        }
    }

    /// <summary>
    /// Pause all the tasks in the pool
    /// </summary>
    public void PauseAll() {
        foreach (Task task in tasks) {
            task.Pause();
        }
    }

    /// <summary>
    /// Resume all the tasks that are paused in the pool
    /// </summary>
    public void UnpauseAll() {
        foreach (Task task in tasks) {
            task.Unpause();
        }
    }

    private bool CheckIfAllAreRunning() {
        bool allRunning = true;
        foreach (Task unused in tasks.Where(_task => !_task.Running)) {
            allRunning = false;
        }

        return allRunning;
    }

    private bool CheckIfAllArePaused() {
        bool allPaused = true;
        foreach (Task unused in tasks.Where(_task => !_task.Paused)) {
            allPaused = false;
        }

        return allPaused;
    }

    private void ATaskFinished(bool _manual) {
        finishedTaskCounter++;
        if (!finished && finishedTaskCounter == tasks.Count) {
            finished = true;
            AllFinishedHandler handler = AllFinished;
            handler?.Invoke();
        }
    }
}

/// <summary>
/// A Task object represents a coroutine.  Tasks can be started, paused, and stopped.
/// It is an error to attempt to start a task that has been stopped or which has naturally terminated.
/// </summary>
public class Task {

    /// <summary>
    /// Delegate for termination subscribers. Manual is true if and only if
    /// the coroutine was stopped with an explicit call to Stop().
    /// </summary>
    public delegate void FinishedHandler(bool _manual);

    private readonly TaskManager.TaskState task;
    
    /// <summary>

    /// </summary>

    
    /// <summary>
    /// Creates a new Task object for the given coroutine.
    /// If autoStart is true (default) the task is automatically started upon construction.
    /// </summary>
    public Task(IEnumerator _task, bool _autoStart = true) {
        task = TaskManager.CreateTask(_task);
        task.Finished += TaskFinished;
        if (_autoStart) {
            Start();
        }
    }


    /// <summary>
    /// Returns true if and only if the coroutine is running. Paused tasks are considered to be running.
    /// </summary>
    public bool Running => task.Running;

    /// <summary>
    /// Returns true if and only if the coroutine is currently paused.
    /// </summary>
    public bool Paused => task.Paused;

    /// <summary>
    /// Termination event. Triggered when the coroutine completes execution.
    /// </summary>
    public event FinishedHandler Finished;

    /// <summary>
    /// Begins execution of the coroutine
    /// </summary>
    public void Start() {
        task.Start();
    }

    /// <summary>
    /// Discontinues execution of the coroutine at its next yield.
    /// </summary>
    public void Stop() {
        task.Stop();
    }

    public void Pause() {
        task.Pause();
    }

    public void Unpause() {
        task.Unpause();
    }

    private void TaskFinished(bool _manual) {
        FinishedHandler handler = Finished;
        handler?.Invoke(_manual);
    }
}

/// <summary>
///     Specialized task for pool, with no auto start
///     This way, all tasks within the pool can be started together
/// </summary>
public class PoolTask : Task {
    public PoolTask(IEnumerator _task) : base(_task, false) { }
}

internal class TaskManager : MonoBehaviour {
    private static TaskManager instance;

    public static TaskState CreateTask(IEnumerator _coroutine) {
        if (instance != null) {
            return new TaskState(_coroutine);
        }

        GameObject go = new("TaskManager");
        instance = go.AddComponent<TaskManager>();

        return new TaskState(_coroutine);
    }

    public class TaskState {
        public delegate void FinishedHandler(bool _manual);

        private readonly IEnumerator coroutine;
        private bool stopped;

        public TaskState(IEnumerator _task) {
            coroutine = _task;
        }

        public bool Running { get; private set; }

        public bool Paused { get; private set; }

        public event FinishedHandler Finished;

        public void Pause() {
            Paused = true;
        }

        public void Unpause() {
            Paused = false;
        }

        public void Start() {
            Running = true;
            instance.StartCoroutine(CallWrapper());
        }

        public void Stop() {
            stopped = true;
            Running = false;
        }

        private IEnumerator CallWrapper() {
            yield return null;
            IEnumerator e = coroutine;
            while (Running) {
                if (Paused) {
                    yield return null;
                } else {
                    if (e != null && e.MoveNext()) {
                        yield return e.Current;
                    } else {
                        Running = false;
                    }
                }
            }

            FinishedHandler handler = Finished;
            handler?.Invoke(stopped);
        }
    }
}