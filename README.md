# Sharpnado.Infrastructure

Sharpnado's infrastructure layer used by https://github.com/roubachof/Sharpnado.Presentation.Forms.

This is all about an extended version of Stephen Cleary's ```NotifyTask``` with:
* Builder pattern
* Callbacks methods ```whenCanceled```, ```whenFaulted```, ```whenCompleted```
* A way of deffering task execution (```isHot```)
* Can pass a ```errorHandler``` (for exception logging)

It's used by the ```Sharpnado.Presentation.Forms``` ```ViewModelLoader```, in order to have a better support for view model's asynchronous initialization.

Here is an excerpt of the ```README.md``` of the ```Sharpnado.Presentation.Forms``` repository:

....

On the Xamarin slack, a question keeps popping:

what is the "good" way of initializing a view model ?

Spoiler alert, this is wrong:

```csharp
public async void Initialize(object parameter)
{
    await InitializationCodeAsync((int)parameter);
}
```

This is a little better:

```csharp
public async void Initialize(object parameter)
{
    try
    {
        await InitializationCodeAsync((int)parameter);
    }
    catch (Exception exception)
    {
        ExceptionHandler.Handle(exception);
    }
}
```

But wait, I want to give a UI feedback to the user:

```csharp
public async void Initialize(object parameter)
{
    IsBusy = true;
    HasErrors = false;
    try
    {
        await InitializationCodeAsync((int)parameter);
    }
    catch (Exception exception)
    {
        ExceptionHandler.Handle(exception);
        HasErrors = true;
        ErrorMessage =
    }
    finally
    {
        IsBusy = false;
    }
}
```

Pfew, this is a lot of copy paste on each of my VM, I will create a base VM for this, and all my VM will inherit from that.

Then stop it, stop that nonsense. Just use [Composition over Inheritance](https://en.wikipedia.org/wiki/Composition_over_inheritance).

The idea is simply to wrap our initialization code in an object responsible for its asynchronous loading.

#### Introducing the ViewModelLoader

Now for the loading part, the issue has been tackled years ago by Stephen Cleary. You should use a ```NotifyTask``` object to wrap your async initialization. It garantees that the exception is correctly caught, and it will notify you (it implements ```INotifyPropertyChanged```).

Continue by reading this: https://msdn.microsoft.com/en-us/magazine/dn605875.aspx.

...

## Open Source licenses

* I greet his grace Stephen Cleary (https://github.com/StephenCleary) who cast his holy words on my async soul (https://www.youtube.com/watch?v=jjaqrPpdQYc). ```NotifyTask``` original code, Copyright (c) 2015 Stephen Cleary, under MIT License (MIT).
