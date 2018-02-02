﻿using System.Collections.Generic;
using System.Linq;
using Force.DeepCloner;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories
{
    public class AtomicUpdateList<T,TS, TT> : IAtomicList<T,TT> where T : class,new()
    {
        private readonly BaseRepository<T,TS,TT> _repo;
        public List<T> EntityList => References.Keys.ToList();

        private Dictionary<T,T> References { get; }=new Dictionary<T, T>();

         
        internal AtomicUpdateList(BaseRepository<T, TS, TT> repo, List<T> objs, bool isUpdate=true)
        {
            _repo = repo;
            IsUpdate = isUpdate;
            if (isUpdate)
            {
                foreach (T t in objs)
                {
                    T n = t.DeepClone();
                    References.Add(n, t);
                }
            }
            else
            {
                foreach (T t in objs)
                {
                    References.Add(t, null);
                }
            }
        }



        public T GetOriginal(T obj)
        {
            if (References.ContainsKey(obj))
                return References[obj];
            return null;
        }





        public void Dispose()
        {
            Release();
        }


        public bool IsUpdate { get;  }

        public List<T> Commit(TT pars = default(TT))
        {
            Dictionary<T, object> savedObjects=new Dictionary<T, object>();
            foreach (T t in EntityList)
            {
                savedObjects[t]=_repo.BeginSave(t, References[t],pars);
            }
            List<T> returns=new List<T>();
            var updates = References.Where(a => a.Value != null).ToList();
            var creates = References.Where(a => a.Value == null).ToList();
            using (_repo.RepoLock.WriterLock())
            {
                foreach (KeyValuePair<T, T> r in updates)
                {
                   
                    r.Key.DeepCloneTo(r.Value);   //Tried to be 100% atomic and failed miserably, so is 99%. 
                                                  //If we replace Original with Entity in cache (updating with 'this' as the model to update will not get the changes).
                                                  //So this is the best effort
                    returns.Add(r.Value);
                }
                foreach (KeyValuePair<T, T> r in creates)
                {
                    _repo.Table.Add(r.Key);
                    returns.Add(r.Key);
                }
                Release();
                if (_repo.IsCached)
                    returns.ForEach(_repo.Cache.Update);
            }
            _repo.Context.SaveChanges();
            foreach (T t in returns)
            {
                _repo.EndSave(t, savedObjects[t], pars);
            }
            return EntityList;
        }


        private void Release()
        {
            References.Clear();
        }
    }
}
