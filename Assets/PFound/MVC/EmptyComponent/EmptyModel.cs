using PFound.MVC.core;
public class EmptyModel : Model<EmptyData>
{
    public EmptyModel(MvcContext context, EmptyData data) : base(context, data) { }

#pragma warning disable CS0618
    public EmptyModel(EmptyData data) : base(data) { }
#pragma warning restore CS0618
}
