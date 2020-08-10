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
    setTimeout(function(){ setServerAddress(window.parent.primaryAddress); }, 2000);
});

function initData()
{
    var apiCmd = "/sb_settings";
    $.getJSON(apiCmd, { format: "json" })
    .done(function (data) {
        var result = data["result"];
        document.getElementsByName("serverName")[1].value = result["serverName"];
        document.getElementsByName("serverDescription")[0].value = result["serverDescription"];
        switch(result["intro"])
        {
            case "1":
                showIntro();
                window.parent.document.getElementById("tab4").firstElementChild.click();
                break;

            case "2":
                showIntroChannels();
                window.parent.document.getElementById("tab4").firstElementChild.click();
                break;

            case "3":
                showIntroGroups();
                window.parent.document.getElementById("tab4").firstElementChild.click();
                break;
		}
        /*document.getElementsByName("serverPassword")[0].value = result["serverPassword"];
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
        document.getElementsByName("defaultChannel")[0].innerHTML = defaultChannel;*/
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
            
            childEl.firstElementChild.setAttribute("onclick", "showEditChannelDialog(this);");

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
            if(result[key]["cost"] != "0.00000000")
            {
                childEl.getElementsByClassName("cost")[0].innerHTML = result[key]["cost"] + ' <i class="fa fa-wallet"></i>';
            }
            var labels = "col-xs-2 labels";
            if(result[key]["admin"] == "True")
            {
                labels += " admin";
            }
            childEl.getElementsByClassName("labels")[0].className = labels;

            childEl.firstElementChild.setAttribute("onclick", "showEditGroupDialog(this);");
            
            groupsHtml += childEl.innerHTML;

            groupSelectorHtml += "<li>" + key + "</li>";
		}

        document.getElementById("GroupList").innerHTML = groupsHtml;

        groupSelectorHtml += "</ul>";
    });
}

function addChannel(rootEl)
{
    var channelName = encodeURIComponent(rootEl.getElementsByClassName("channelName")[0].value);
    var channelDefault = 0;
    if(rootEl.getElementsByClassName("channelDefault").length == 0 || rootEl.getElementsByClassName("channelDefault")[0].checked)
    {
        channelDefault = 1;
	}
    var action = "sb_newChannel";
    var origChannel = "";
    if(rootEl.getElementsByClassName("origChannelName").length > 0)
    {
        origChannel = encodeURIComponent(rootEl.getElementsByClassName("origChannelName")[0].value);
        action = "sb_updateChannel";
	}
    var apiCmd = "/" + action + "?channel=" + channelName + "&default=" + channelDefault + "&origChannel=" + origChannel;
    hideEditChannelDialog();
    $.getJSON(apiCmd, { format: "json" })
    .done(function (data) {
        getChannels();
    });
}

function addGroup(rootEl)
{
    var groupName = encodeURIComponent(rootEl.getElementsByClassName("groupName")[0].value);
    var messageCost = rootEl.getElementsByClassName("groupMessageCost")[0].value;
    var admin = 0;
    if(rootEl.getElementsByClassName("groupAdmin")[0].checked)
    {
        admin = 1;
    }
    var groupDefault = 0;
    if(rootEl.getElementsByClassName("groupDefault").length == 0 || rootEl.getElementsByClassName("groupDefault")[0].checked)
    {
        groupDefault = 1;
	}
    var action = "sb_newGroup";
    var origGroup = "";
    if(rootEl.getElementsByClassName("origGroupName").length > 0)
    {
        origGroup = encodeURIComponent(rootEl.getElementsByClassName("origGroupName")[0].value);
        action = "sb_updateGroup";
	}
    var apiCmd = "/" + action + "?group=" + groupName + "&cost=" + messageCost + "&admin=" + admin + "&default=" + groupDefault + "&origGroup=" + origGroup;
    hideEditGroupDialog();
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
            setUserGroup(e.target.previousElementSibling.innerHTML, ev.target.innerHTML);
        }
        groupSelectorEl.parentNode.removeChild(groupSelectorEl);
        groupSelectorEl = null;
	};
    groupSelectorEl.innerHTML = groupSelectorHtml;
    var rect = e.target.getBoundingClientRect();
    groupSelectorEl.style.left = (rect.left + 5) + "px";
    groupSelectorEl.style.top = (rect.bottom - 10) + "px";

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

function saveAvatar(el)
{
    if(el.files != null && el.files.length > 0)
    {
        readFile(el.files[0], "image");
    }
}

function readFile(file, type)
{
    // Check if the file is an image.
    if (file.type && file.type.indexOf(type) === -1) {
        console.log('File is not an image.', file.type, file);
        return;
    }

    const reader = new FileReader();
    reader.addEventListener('load', (event) => {
        var data = reader.result;
        var apiCmd = "/";
        $.post(apiCmd, JSON.stringify({ method: "sb_saveAvatar", params: { data: data } }))
        .done(function (data) {
            document.getElementsByClassName("serverAvatar")[0].style.backgroundImage = "url(/resources/avatar.jpg?" + Math.random() + ")";
        });
    });
    reader.readAsDataURL(file);
}

function showEditChannelDialog(listEl)
{
    var channelName = listEl.getElementsByClassName("channel-name")[0].innerHTML;

    var el = document.getElementById("ChannelEditBoxModal");
    el.style.display = "block";
    el.getElementsByClassName("origChannelName")[0].value = channelName;
    el.getElementsByClassName("channelName")[0].value = channelName;
    el.getElementsByClassName("channelDefault")[0].checked = false;
}

function hideEditChannelDialog()
{
    document.getElementById("ChannelEditBoxModal").style.display = "none";
}

function showEditGroupDialog(listEl)
{
    var groupName = listEl.getElementsByClassName("group-name")[0].innerHTML;
    var groupCost = listEl.getElementsByClassName("cost")[0].innerHTML;
    var admin = false;
    if(listEl.getElementsByClassName("admin").length > 0)
    {
         admin = true;
	}

    var el = document.getElementById("GroupEditBoxModal");
    el.style.display = "block";
    el.getElementsByClassName("origGroupName")[0].value = groupName;
    el.getElementsByClassName("groupName")[0].value = groupName;
    el.getElementsByClassName("groupMessageCost")[0].value = groupCost.substring(0, groupCost.indexOf("<"));
    if(admin)
    {
        el.getElementsByClassName("groupAdmin")[0].checked = true;
    }else
    {
        el.getElementsByClassName("groupAdmin")[0].checked = false;
	}
    el.getElementsByClassName("groupDefault")[0].checked = false;
}

function hideEditGroupDialog()
{
    document.getElementById("GroupEditBoxModal").style.display = "none";
}

function validateInput(el, submitEl)
{
    if(el.value != "")
    {
        submitEl.className = submitEl.className.replace("disabled", "");
	}
}


var qrcode = new QRCode(document.getElementById("qrcode"), {
        width: 200,
        height: 200
    });

function setServerAddress(address)
{
    document.getElementById("ServerAddress").innerHTML = address;

    // Create the QR code
    qrcode.clear();
    qrcode.makeCode(address);
}

function deleteGroup(e, groupEl)
{
    e.stopPropagation();
    var groupName = groupEl.getElementsByClassName("group-name")[0].innerHTML;
    var groupNameEncoded = encodeURIComponent(groupName);
    var leftButton = "<div onclick=\"hideModalDialog();\">CANCEL</div>";
    var rightButton = "<div onclick=\"doDeleteGroup('" + groupNameEncoded + "');\">REMOVE GROUP</div>";
    showModalDialog("Remove group " + groupName + "?", "Are you sure you want to remove the " + groupName + " group? This group will be permanently removed.", leftButton, rightButton);
}

function doDeleteGroup(groupName)
{
    hideModalDialog();
    $.getJSON("/sb_delGroup?group=" + groupName, function()
    {
        getGroups();
	});
}

function deleteChannel(e, channelEl)
{
    e.stopPropagation();
    var channelName = channelEl.getElementsByClassName("channel-name")[0].innerHTML;
    var channelNameEncoded = encodeURIComponent(channelName);
    var leftButton = "<div onclick=\"hideModalDialog();\">CANCEL</div>";
    var rightButton = "<div onclick=\"doDeleteChannel('" + channelNameEncoded + "');\">REMOVE CHANNEL</div>";
    showModalDialog("Remove channel #" + channelName + "?", "Are you sure you want to remove the #" + channelName + " channel? This channel and all message history in it will be permanently removed.", leftButton, rightButton);
}

function doDeleteChannel(channelName)
{
    hideModalDialog();
    $.getJSON("/sb_delChannel?channel=" + channelName, function()
    {
        getChannels();
	});
}

function kickUser(e, userEl)
{
    e.stopPropagation();
    var address = userEl.getElementsByClassName("address")[0].innerHTML;
    var addressEncoded = encodeURIComponent(address);
    var userEncoded = encodeURIComponent(userEl.getElementsByClassName("nick")[0].innerHTML);
    var leftButton = "<div onclick=\"hideModalDialog();\">CANCEL</div>";
    var rightButton = "<div onclick=\"doKickUser('" + addressEncoded + "');\">KICK USER</div>";
    showModalDialog("Kick user " + userEncoded + "?", "Are you sure you want to kick user " + userEncoded + "?", leftButton, rightButton);
}

function doKickUser(address)
{
    hideModalDialog();
    $.getJSON("/sb_kickUser?address=" + address, function()
    {
        getUsers();
	});
}

function banUser(e, userEl)
{
    e.stopPropagation();
    var address = userEl.getElementsByClassName("address")[0].innerHTML;
    var addressEncoded = encodeURIComponent(address);
    var userEncoded = encodeURIComponent(userEl.getElementsByClassName("nick")[0].innerHTML);
    var leftButton = "<div onclick=\"hideModalDialog();\">CANCEL</div>";
    var rightButton = "<div onclick=\"doBanUser('" + addressEncoded + "');\">BAN USER</div>";
    showModalDialog("Ban user " + userEncoded + "?", "Are you sure you want to ban user " + userEncoded + "?", leftButton, rightButton);
}

function doBanUser(address)
{
    hideModalDialog();
    $.getJSON("/sb_banUser?address=" + address, function()
    {
        getUsers();
	});
}


var modalHtml = '<div class="modal-content" onclick="event.stopPropagation(); return false;">\
                <div class="spixi-modal-header warn">\
                </div>\
                <hr class="spixi-separator noheightmargins fullwidth" />\
                \
                <div class="spixi-modal-text">\
                </div>\
                \
                <hr class="spixi-separator noheightmargins fullwidth" />\
                <div class="spixi-modal-footer">\
                    <div class="spixi-modal-button-left"></div>\
                    <div class="spixi-modal-button-right"></div>\
                </div>\
        </div>';

function showModalDialog(title, body, leftButton, rightButton){
    hideModalDialog();

    var modalEl = document.createElement("div");
    modalEl.id = "SpixiModalDialog";
    modalEl.className = "spixi-modal";
    modalEl.innerHTML = modalHtml;
    modalEl.onclick = hideModalDialog;

    modalEl.getElementsByClassName("spixi-modal-header")[0].innerHTML = title;
    modalEl.getElementsByClassName("spixi-modal-text")[0].innerHTML = body;

    modalEl.getElementsByClassName("spixi-modal-button-left")[0].innerHTML = leftButton;
    modalEl.getElementsByClassName("spixi-modal-button-right")[0].innerHTML = rightButton;

    document.body.appendChild(modalEl);
    modalEl.style.display = "block";
}

function hideModalDialog()
{
    var modalEl = document.getElementById("SpixiModalDialog");
    if(modalEl != null)
    {
        document.body.removeChild(modalEl);
	}
}

function showIntro()
{
    document.getElementsByClassName("intro")[0].style.display = "block";
}

function showIntroChannels()
{
    document.getElementsByClassName("intro")[0].style.display = "none";
    document.getElementsByClassName("intro-channels")[0].style.display = "block";
}

function showIntroGroups()
{
    document.getElementsByClassName("intro-channels")[0].style.display = "none";
    document.getElementsByClassName("intro-groups")[0].style.display = "block";
}

function finishIntro()
{
    document.getElementsByClassName("intro")[0].style.display = "none";
    document.getElementsByClassName("intro-channels")[0].style.display = "none";
    document.getElementsByClassName("intro-groups")[0].style.display = "none";
}