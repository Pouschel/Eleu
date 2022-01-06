var ελευ = require('./EleuCore');
var a = ελευ.CreateNumberVal(1);

{
	var b = ελευ.CreateNumberVal(2);
	a = ελευ.CreateStringVal("Hallo");
	{
		var c = ελευ.CreateNumberVal(3);
	}
	console.log((b).toString());
	console.log((a + b).toString());
}

