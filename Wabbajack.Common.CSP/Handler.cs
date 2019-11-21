namespace Wabbajack.Common.CSP
{
    public interface Handler<T>
    {
        /// <summary>
        /// Returns true if this handler has a callback, must work without a lock
        /// </summary>
        bool IsActive { get;  }

        /// <summary>
        /// Returns true if this handler may be blocked, otherwise it must not block
        /// </summary>
        bool IsBlockable { get;  }

        /// <summary>
        /// A unique id for lock aquisition order, 0 if no lock
        /// </summary>
        uint LockId { get; }

        /// <summary>
        /// Commit to fulfilling its end of the transfer, returns cb, must be called within a lock
        /// </summary>
        /// <returns>A callback</returns>
        T Commit();
    }

}
