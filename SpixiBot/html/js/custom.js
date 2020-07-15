function initSpixiBot()
{
	document.getElementById("send").parentNode.innerHTML += '<iframe id="settings" src="resources/settings.html" class="tab-pane fade"></iframe>';

	document.getElementById("tab1").className = "col-xs-3 text-center";
	document.getElementById("tab2").className = "col-xs-3 text-center active";
	document.getElementById("tab3").className = "col-xs-3 text-center";

	document.getElementById("bottomNav").innerHTML += '<div id="tab4" class="col-xs-3 text-center"><a data-toggle="tab" href="#settings"><img src="resources/img/bot/settings.png" /><br>SETTINGS</a></div>';
}

initSpixiBot();
