var groupSelectorHtml = "";
var groupSelectorEl = null;

var channelSelectorHtml = "";
var channelSelectorEl = null;

function initTabs()
{
    // Function to toggle tab's active color
    $('a[data-toggle="tab"]').on('shown.bs.tab', function (e) {
        var nodes = document.getElementById("SideMenu").childNodes;
        for(var childIndex = 0; childIndex < nodes.length; childIndex++)
        {
            if(nodes[childIndex].nodeName.toLowerCase() != "a")
            {
                continue;     
			}
            nodes[childIndex].className = nodes[childIndex].className.replace("active", "").trim();
        }
        e.target.className += " active";
        if(e.target.getAttribute("href") == "#General")
        {
            initData();
		}

        if(e.target.getAttribute("href") == "#Users")
        {
            getUsers();
		}

        if(e.target.getAttribute("href") == "#Groups")
        {
            getGroups();
		}

        if(e.target.getAttribute("href") == "#Channels")
        {
            getChannels();
		}

    });
}


$(function () {
    initTabs();
    initData();
    getGroups();
});

function initData()
{
    var apiCmd = "/sb_settings";
    $.getJSON(apiCmd, { format: "json" })
    .done(function (data) {
        var result = data["result"];
        document.getElementsByName("serverName")[0].value = result["serverName"];
        document.getElementsByName("serverPassword")[0].value = result["serverPassword"];
        if(result["allowFileTransfer"] == "1")
        {
            document.getElementsByName("allowFileTransfer")[0].checked = true;
		}else
        {
            document.getElementsByName("allowFileTransfer")[0].checked = false;
		}
        document.getElementsByName("fileTransferLimitMB")[0].value = result["fileTransferLimitMB"];
        
        var defaultGroup = result["defaultGroup"];
        if(defaultGroup == "")
        {
            defaultGroup = "None Specified";  
		}
        document.getElementsByName("defaultGroup")[0].innerHTML = defaultGroup;

        var defaultChannel = result["defaultChannel"];
        if(defaultChannel == "")
        {
            defaultChannel = "None Specified";  
		}
        document.getElementsByName("defaultChannel")[0].innerHTML = defaultChannel;
    });
}

function setOption(category, inputEl)
{
    if(category == "general")
    {
        var value = inputEl.value;
        if(inputEl.type == "checkbox" && !inputEl.checked)
        {
            value = 0;
		}
        var apiCmd = "/sb_setOption?" + inputEl.name + "=" + value;
        $.getJSON(apiCmd, { format: "json" })
        .done(function (data) {
        });
	}
}

function setOptionWithValue(category, name, value)
{
    if(category == "general")
    {
        var apiCmd = "/sb_setOption?" + name + "=" + value;
        $.getJSON(apiCmd, { format: "json" })
        .done(function (data) {
        });
	}
}

function getUsers()
{
    var apiCmd = "/sb_getUsers";
    $.getJSON(apiCmd, { format: "json" })
    .done(function (data) {
        var userTemplate = document.getElementsByClassName("user")[0].innerHTML;

        var usersHtml = "";
        var result = data["result"];
        var childEl = document.createElement("div");
        for(var key in result)
        {
            childEl.innerHTML = userTemplate;

            childEl.getElementsByClassName("address")[0].innerHTML = key;
            childEl.getElementsByClassName("nick")[0].innerHTML = result[key]["nick"];
            var role = result[key]["role"];
            if(role == "")
            {
                role = "[DEFAULT]";     
			}
            childEl.getElementsByClassName("role")[0].innerHTML = role;
            
            usersHtml += childEl.innerHTML;
		}

        document.getElementById("UserList").innerHTML = usersHtml;
    });
}

function getChannels()
{
    var apiCmd = "/sb_getChannels";
    $.getJSON(apiCmd, { format: "json" })
    .done(function (data) {
        var userTemplate = document.getElementsByClassName("channel")[0].innerHTML;

        channelSelectorHtml = "<ul>";

        var channelsHtml = "";
        var result = data["result"];
        var childEl = document.createElement("div");
        for(var key in result)
        {
            childEl.innerHTML = userTemplate;

            childEl.getElementsByClassName("channel-name")[0].innerHTML = key;
            
            channelsHtml += childEl.innerHTML;

            channelSelectorHtml += "<li>" + key + "</li>";
		}

        document.getElementById("ChannelList").innerHTML = channelsHtml;

        channelSelectorHtml += "</ul>";
    });
}

function getGroups()
{
    var apiCmd = "/sb_getGroups";
    $.getJSON(apiCmd, { format: "json" })
    .done(function (data) {
        var userTemplate = document.getElementsByClassName("group")[0].innerHTML;

        groupSelectorHtml = "<ul>";
        groupSelectorHtml += "<li class='defaultGroup'>[DEFAULT]</li>";

        var groupsHtml = "";
        var result = data["result"];
        var childEl = document.createElement("div");
        for(var key in result)
        {
            childEl.innerHTML = userTemplate;

            childEl.getElementsByClassName("group-name")[0].innerHTML = key;
            childEl.getElementsByClassName("cost")[0].innerHTML = result[key]["cost"];
            childEl.getElementsByClassName("admin")[0].innerHTML = result[key]["admin"];
            
            groupsHtml += childEl.innerHTML;

            groupSelectorHtml += "<li>" + key + "</li>";
		}

        document.getElementById("GroupList").innerHTML = groupsHtml;

        groupSelectorHtml += "</ul>";
    });
}

function addChannel()
{
    var channelName = document.getElementsByName("channelName")[0].value;
    var apiCmd = "/sb_newChannel?channel=" + channelName;
    $.getJSON(apiCmd, { format: "json" })
    .done(function (data) {
        getChannels();
    });
}

function addGroup()
{
    var groupName = document.getElementsByName("groupName")[0].value;
    var messageCost = document.getElementsByName("groupMessageCost")[0].value;
    var admin = 0;
    if(document.getElementsByName("groupAdmin")[0].checked)
    {
        admin = 1;
    }
    var apiCmd = "/sb_newGroup?group=" + groupName + "&cost=" + messageCost + "&admin=" + admin;
    $.getJSON(apiCmd, { format: "json" })
    .done(function (data) {
        getGroups();
    });
}

function setUserGroup(address, group)
{
    var apiCmd = "/sb_setUserGroup?address=" + address + "&role=" + group;
    $.getJSON(apiCmd, { format: "json" })
    .done(function (data) {
        getUsers();
    });
}

function displayGroupSelector(e)
{
    if(groupSelectorEl != null)
    {
        groupSelectorEl.parentNode.removeChild(groupSelectorEl);
	}
    groupSelectorEl = document.createElement("div");
    groupSelectorEl.className = "groupSelector";
    if(e.target.id == "DefaultGroup")
    {
        groupSelectorEl.className += " hideDefaultGroup";
	}

    groupSelectorEl.onclick = function(ev)
    {
        if(e.target.id == "DefaultGroup")
        {
            setOptionWithValue("general", "defaultGroup", ev.target.innerHTML);
            initData();
	    }else
        {
            setUserGroup(e.target.previousElementSibling.previousElementSibling.innerHTML, ev.target.innerHTML);
        }
        groupSelectorEl.parentNode.removeChild(groupSelectorEl);
        groupSelectorEl = null;
	};
    groupSelectorEl.innerHTML = groupSelectorHtml;
    groupSelectorEl.style.left = e.clientX + "px";
    groupSelectorEl.style.top = e.clientY + "px";

    document.body.appendChild(groupSelectorEl);
}

function displayChannelSelector(e)
{
    if(channelSelectorEl != null)
    {
        channelSelectorEl.parentNode.removeChild(channelSelectorEl);
	}
    channelSelectorEl = document.createElement("div");
    channelSelectorEl.className = "channelSelector";

    channelSelectorEl.onclick = function(ev)
    {
        setOptionWithValue("general", "defaultChannel", ev.target.innerHTML);
        initData();

        channelSelectorEl.parentNode.removeChild(channelSelectorEl);
        channelSelectorEl = null;
	};
    channelSelectorEl.innerHTML = channelSelectorHtml;
    channelSelectorEl.style.left = e.clientX + "px";
    channelSelectorEl.style.top = e.clientY + "px";

    document.body.appendChild(channelSelectorEl);
}