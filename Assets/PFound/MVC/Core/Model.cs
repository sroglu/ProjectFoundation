using System;
using System.Collections.Generic;

namespace PFound.MVC.core
{
    public interface IModel : IDisposable
    {
        uint InstanceId(int subModelIndex = 0);
    }

    public enum ModelType
    {
        Single,
        Array
    }
    public abstract class ModelBase : IModel, ICloneable
    {
        public uint instanceId { get { return _instanceId; } }
        uint _instanceId = 0;
        public bool Initiated { get { return instanceId > 0; } }
        public ModelType modelType { get { return _modelType; } }
        ModelType _modelType = ModelType.Array;

        /// <summary>
        /// The scoped context this model is registered with (null for legacy/array models).
        /// </summary>
        protected MvcContext Context { get; }

        #region Legacy static registry (backward compat)
        [Obsolete("Use MvcContext model registry instead")]
        protected static readonly Dictionary<uint, IModel> modelDictionary = new Dictionary<uint, IModel>();

        [Obsolete("Use MvcContext.GetModel<T>() or MvcContext.GetModelById() instead")]
        public static IModel GetModelByInstanceId(uint instanceId)
        {
            return modelDictionary[instanceId];
        }

        [Obsolete("Use MvcContext.Dispose() for cleanup")]
        public static void ResetDictionary()
        {
            modelDictionary.Clear();
        }
        #endregion

        protected uint RegisterNewModel()
        {
            if (Context != null)
            {
                _instanceId = Context.RegisterModel(this);
            }
            else
            {
#pragma warning disable CS0618
                _instanceId = (uint)modelDictionary.Count + 1;
                modelDictionary.Add(_instanceId, this);
#pragma warning restore CS0618
            }
            _modelType = ModelType.Single;
            return _instanceId;
        }

        /// <summary>
        /// Constructor with scoped context.
        /// </summary>
        protected ModelBase(MvcContext context)
        {
            Context = context;
        }

        /// <summary>
        /// Backward-compatible constructor — uses legacy static dictionary.
        /// </summary>
        protected ModelBase()
        {
            Context = null;
        }

        public virtual void Dispose()
        {
            UnregisterModel();
        }
        void UnregisterModel()
        {
            if (modelType == ModelType.Single)
            {
                if (Context != null)
                {
                    Context.UnregisterModel(instanceId);
                }
                else
                {
#pragma warning disable CS0618
                    if (modelDictionary.ContainsKey(instanceId))
                        modelDictionary.Remove(instanceId);
#pragma warning restore CS0618
                }
            }
        }

        public abstract uint InstanceId(int subModelIndex);
        public object Clone()
        {
            return MemberwiseClone();
        }

    }

    [System.Serializable]
    public abstract class Model<T> : ModelBase where T : ICloneable
    {
        public override uint InstanceId(int subModelIndex = 0)
        {
            if (subModels != null)
                return subModels[subModelIndex].InstanceId();
            return instanceId;
        }

        [Obsolete("Use MvcContext.GetModel<T>() instead")]
        public static new Model<T> GetModelByInstanceId(uint instanceId)
        {
#pragma warning disable CS0618
            return ModelBase.GetModelByInstanceId(instanceId) as Model<T>;
#pragma warning restore CS0618
        }

        protected T DescriptionData { get; private set; }
        protected T[] DescriptionDataArr { get; private set; }
        public T CurrentData { get; protected set; }
        public T[] CurrentDataArr { get; protected set; }
        public Model<T>[] SubModels { get { return subModels; } }
        private Model<T>[] subModels;

        /// <summary>
        /// Constructor with scoped context.
        /// </summary>
        public Model(MvcContext context, T data) : base(context)
        {
            DescriptionData = data;
            UpdateCurrentData();
            RegisterNewModel();
        }

        /// <summary>
        /// Backward-compatible constructor — uses legacy static dictionary.
        /// </summary>
        [Obsolete("Use the constructor with MvcContext parameter")]
        public Model(T data) : base()
        {
            DescriptionData = data;
            UpdateCurrentData();
            RegisterNewModel();
        }

        /// <summary>
        /// Constructor with scoped context for array data.
        /// </summary>
        public Model(MvcContext context, T[] dataArr) : base(context)
        {
            DescriptionDataArr = dataArr;
            UpdateCurrentData();
            CreateSubModels(out subModels);
        }

        /// <summary>
        /// Backward-compatible constructor for array data.
        /// </summary>
        [Obsolete("Use the constructor with MvcContext parameter")]
        public Model(T[] dataArr) : base()
        {
            DescriptionDataArr = dataArr;
            UpdateCurrentData();
            CreateSubModels(out subModels);
        }

        #region Operations
        public void Update(T data)
        {
            CurrentData = data;
            UpdateDescriptionData();
        }
        public void Update(T[] data)
        {
            CurrentDataArr = data;
            UpdateDescriptionData();
        }
        public void UpdateCurrentData()
        {
            if (DescriptionData != null)
                UpdateCurrentData(DescriptionData);

            if (DescriptionDataArr != null)
                UpdateCurrentData(DescriptionDataArr);
        }
        void UpdateCurrentData(T data)
        {
            CurrentData = (T)data.Clone();
        }
        void UpdateCurrentData(T[] dataArr)
        {
            Array.Copy(dataArr, CurrentDataArr, dataArr.Length);
        }

        public void UpdateDescriptionData()
        {
            if (CurrentData != null)
                UpdateDescriptionData(CurrentData);

            if (CurrentDataArr != null)
                UpdateDescriptionData(CurrentDataArr);
        }
        void UpdateDescriptionData(T data)
        {
            DescriptionData = (T)data.Clone();
        }
        void UpdateDescriptionData(T[] dataArr)
        {
            Array.Copy(dataArr, DescriptionDataArr, dataArr.Length);
        }
        #endregion


        protected virtual void CreateSubModels(out Model<T>[] subModels) { subModels = null; }
    }
}
