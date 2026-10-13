using ElevenWeather.ViewModels;

namespace ElevenWeather.Tests.ViewModels;

public sealed class MvvmInfrastructureTests
{
    private sealed class Sample : ObservableObject
    {
        private int _value;

        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public bool LastSetChanged { get; private set; }

        public void SetTracked(int value) => LastSetChanged = SetProperty(ref _value, value, nameof(Value));
    }

    [Fact]
    public void SetProperty_NotifiesOnlyWhenValueChanges()
    {
        var sample = new Sample();
        var changed = new List<string?>();
        sample.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        sample.Value = 1;
        sample.Value = 1;
        sample.Value = 2;

        Assert.Equal(new[] { "Value", "Value" }, changed);
    }

    [Fact]
    public void SetProperty_ReturnsWhetherValueChanged()
    {
        var sample = new Sample();

        sample.SetTracked(5);
        Assert.True(sample.LastSetChanged);

        sample.SetTracked(5);
        Assert.False(sample.LastSetChanged);
    }

    [Fact]
    public void RelayCommand_ExecutesAction()
    {
        int calls = 0;
        var command = new RelayCommand(() => calls++);

        command.Execute(null);

        Assert.Equal(1, calls);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void RelayCommand_UsesCanExecutePredicate()
    {
        bool allowed = false;
        var command = new RelayCommand(() => { }, () => allowed);

        Assert.False(command.CanExecute(null));
        allowed = true;
        Assert.True(command.CanExecute(null));
    }
}
