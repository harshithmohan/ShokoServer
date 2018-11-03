﻿using System;
using Force.DeepCloner;
using Microsoft.EntityFrameworkCore;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories
{
    public class AtomicUpdate<T,TS,TT> : IAtomic<T,TT> where T : class, new()
    {
        private readonly BaseRepository<T,TS,TT> _repo;
        public T Entity { get; private set; }
        public T Original { get; private set; }


        internal AtomicUpdate(BaseRepository<T, TS,TT> repo, T obj=null, bool isupdate=true)
        {
            _repo = repo;
            Original = obj;
            if (obj == null)
            {
                Entity = new T();
                IsUpdate = false;
            }
            else if (!isupdate)
            {
                Entity = obj;
                IsUpdate = false;
            }
            else
            {
                Entity = obj.DeepClone();
                IsUpdate = true;
            }
        }
        public void Dispose()
        {
            Release();
        }


        public bool IsUpdate { get;  }

        public T Commit(TT pars=default(TT)) 
        // Pars are the extra parameters send to the save and delete callbacks, in this way we can forward behaviors to the callbacks
        {
            object obj=_repo.BeginSave(Entity, Original, pars);
            T ret;
            using (_repo.RepoLock.WriterLock())
            {
                ShokoContext ctx = _repo.Provider.GetContext();
                if (Original == null || !IsUpdate)
                {
                    ret = Entity;
                    ctx.Add(Entity);
                }
                else
                {
                    ret = Original;
                    ctx.UpdateChanges(Original,Entity);
/*
                    Entity.DeepCloneTo(Original); //Tried to be 100% atomic and failed miserably, so is 99%. 
                                                  //If we replace Original with Entity in cache (updating with 'this' as the model to update will not get the changes).
                                                  //So this is the best effort
                    ctx.Attach(Original);
                    ctx.Update(Original);*/
                }
                Release();
                if (_repo.IsCached)
                    _repo.Cache.Update(ret);
                ctx.SaveChanges();
                ctx.Detach(ret);
            }
            if (obj!=null)
                _repo.EndSave(ret, obj, pars);
            return ret;
        }

       

        public void Release()
        {
            Entity = null;
            Original = null;
        }

    }
}
