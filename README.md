# SignalARRR

SignalARRR adds a few useful things to the Default SignalR Library, at least in my opinion...

* split/seperate Methods in multiple Classes (inherit from ServerMethods\<T\>)
* use authorize Attribute on single methods, or on the Class
* authentication happens on each method call, which allows to check if `Reference Token` is rejected.
* invoke client methods from the Server and get a response
* use Observable\<T\> as return type

There is a Server Part, a .Net Client and a Typescript Client, which implements those Features.


Library is still work in Progress!

More documentation is comming soon...

