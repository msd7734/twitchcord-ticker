// Global constants
// These can be changed to tweak the animation
const _aestheticOffset = 5;         // Default: 5
const _maxScrollSpeedMs = 15000;    // Default: 11000, Try: 15000
const _minScrollSpeedMs = 3000;     // Default: 4000, Try: 3000
const _scrollSpeedStepMs = 250;     // Default: 500, Try: 250
const _pixelsPerStep = 50;         // Default: 100, Try: 50
const _transitionFadePx = 10;       // Default: 10
const _transitionFadeMs = 500;      // Default: 500
const _minUpdateTime = 2900;        // Default: 3000, Try: 2900


// These are used to tweak functionality
const _cleanChars = true;           // Default: true
const _censorLanguage = false;      // Default: false
const _censorType = 1;              // Default: 1, 0 is delete, 1 is bleep
const _defaultMsg = "Welcome to the stream. Users in approved channels on Discord can use !m to update the ticker.";

// Don't touch these
const _pollRateMs = 500;
const _localPort = 9000;

const SCROLL_DELAY_DEFAULT = "scroll-left";
const SCROLL_DELAY_SHORT = "scroll-left-delay-short";
const SCROLL_DELAY_MED = "scroll-left-delay-medium";
const SCROLL_DELAY_LONG = "scroll-left-delay-long";

const SCROLL_DELAY_CLASS_DEFAULT = "ticker-scroll";
const SCROLL_DELAY_CLASS_SHORT = "ticker-scroll-delay-short";
const SCROLL_DELAY_CLASS_MED = "ticker-scroll-delay-medium";
const SCROLL_DELAY_CLASS_LONG = "ticker-scroll-delay-long";

var _curScrollDelay = SCROLL_DELAY_DEFAULT;

var _queue = [];
var _qLock = false;
var _updateDelay = _minScrollSpeedMs;
var _prevDelay = _updateDelay;
var _scrolledOnce = false;

function clamp(n, min, max) {
    return Math.max(min, Math.min(n, max))
}

function getKeyframeRule(ruleName) {
    let ss = document.styleSheets;
    for (let i = 0; i < ss.length; ++i) {
        for (let sheet = 0; sheet < ss[i].cssRules.length; ++sheet) {
            let rule = ss[i].cssRules[sheet];
            if (rule.name === ruleName && rule.type == CSSRule.KEYFRAMES_RULE) {
                return rule;
            }
        }
    }
    return null;
}

function getCSSRule(selector) {
    let ss = document.styleSheets;
    for (let i = 0; i < ss.length; ++i) {
        for (let sheet = 0; sheet < ss[i].cssRules.length; ++sheet) {
            let rule = ss[i].cssRules[sheet];
            if (rule.selectorText === selector) {
                return rule;
            }
        }
    }
    return null;
}

/*
    CURRENTLY NOT USED
*/
function getDelayRules() {
    let kf = getKeyframeRule(_curScrollDelay);
    if (!kf) {
        return null;
    }

    let startDelayRules = kf.cssRules[0].keyText
        .split(",")
        .map(r => r.slice(0,-1)*1);

    
    const startDelay = startDelayRules[1] - startDelayRules[0];

    let endDelayRules = kf.cssRules[1].keyText
        .split(",")
        .map(r => r.slice(0,-1)*1);

    const endDelay = endDelayRules[1] - endDelayRules[0]; 

    return {
        startPercent: startDelay / 100,
        endPercent: endDelay / 100
    };
}

/*
    Given how much time the ticker will be scrolling for (including delay),
    return how long it should wait before updating from the new message queue.
*/
function calcUpdateDelay(realScrollMs) {
    // if the text can fit in the frame, transition delay should be a function
    //  of how big it is compared to its container

    const txtWidth = getTickerTextWidth()
    const tickerWidth = getTickerWidth()

    if (tickerWidth > txtWidth) {
        const min = _minUpdateTime;
        const max = _minScrollSpeedMs;
        const percent = (txtWidth / tickerWidth);
        console.log("Reduce update delay by " + percent + "%");

        return min + ((max - min) * percent)

        // for every 200 pixels of text width, decrease the min scroll time by 100ms
        //return clamp(realScrollMs - ((textWidth / 200) * 100), _minUpdateTime, _minScrollSpeedMs);
    }
    else {
        // Don't try and pare down the time here, when the text scrolls
        //  it takes more time and attention to visually parse
        //  Subtract some milliseconds to prevent the text from jumping back to the start
        //  right before it transitions
        //  NOTE: The constant 1/5 is based on tweaking and could look bad if the percentages
        //          for the scroll delay change too drastically
        const offsetStep = (_maxScrollSpeedMs - _minScrollSpeedMs) / 5;
        let offset = (function(step) {
            switch(_curScrollDelay) {
                // Recheck the logic here... Should we multiply by 0.8 and 0.6 ...? 
                case SCROLL_DELAY_CLASS_SHORT:
                    return step * 1.2;
                case SCROLL_DELAY_CLASS_MED:
                    return step * 1.4;
                case SCROLL_DELAY_LONG:
                default:
                    return step;
            }
        })(offsetStep);

        return realScrollMs - offset;
    }
}

/*
    Because there is an animation delay at the beginning and end of the scroll,
    having the animation play for 1000ms for example will not reflect the true
    amount of time spent scrolling, as some amount will be used up during the pauses.
    
    This returns the real required to scroll for timeMs given the pause times in the
    scroll-left* animations.
*/
function calcRealTranslateTime(timeMs) {
    let kf = getKeyframeRule(_curScrollDelay);
    if (!kf) {
        return timeMs;
    }
    
    // this is hardcoded because it's meant specifically for the scroll-left* animations
    // it will specifically have two rules in the format:
    //  0%,X%
    //  Y%,100%
    // Where X and Y are when the start delay ends and the end delay begins respectively
    let startDelayRules = kf.cssRules[0].keyText
        .split(",")
        .map(r => r.slice(0,-1)*1);    // remove % char and coerce to int

    const startDelay = startDelayRules[1] - startDelayRules[0];

    let endDelayRules = kf.cssRules[1].keyText
        .split(",")
        .map(r => r.slice(0,-1)*1);    // remove % char and coerce to int

    const endDelay = endDelayRules[1] - endDelayRules[0];

    return timeMs / ((100 - (startDelay + endDelay)) / 100);
}

/*
    Get the name of the scroll animation class based on the target (non-actual)
    scroll time. Meaning, pass the target time BEFORE calcRealTranslateTime has been
    called.
*/
function getScrollDelay(timeMs) {
    const third = (_maxScrollSpeedMs - _minScrollSpeedMs) / 3;
    const firstThird = _minScrollSpeedMs + third;
    const secondThird = firstThird + third;

    if (_minScrollSpeedMs <= timeMs && timeMs < firstThird) {
        return SCROLL_DELAY_LONG;
    }
    else if (firstThird <= timeMs && timeMs < secondThird) {
        return SCROLL_DELAY_MED;
    }
    else if (secondThird <= timeMs && timeMs <= _maxScrollSpeedMs) {
        return SCROLL_DELAY_SHORT;
    }
    else {
        // fallback case
        console.warn("Potential invalid scroll time in getScrollDelay(): " + timeMs);
        return SCROLL_DELAY_DEFAULT;
    }
}

function getScrollDelayClass(delayType) {
    switch(delayType) {
        case SCROLL_DELAY_SHORT: return SCROLL_DELAY_CLASS_SHORT;
        case SCROLL_DELAY_MED: return SCROLL_DELAY_CLASS_MED;
        case SCROLL_DELAY_LONG: return SCROLL_DELAY_CLASS_LONG;
        default: return SCROLL_DELAY_CLASS_DEFAULT;
    }
}


/*
    Generic method using canvas for any given text
*/
function getTextWidth(text, font) {
    // re-use canvas object for better performance
    var canvas = getTextWidth.canvas || (getTextWidth.canvas = document.createElement("canvas"));
    var context = canvas.getContext("2d");
    context.font = font;
    let metrics = context.measureText(text);
    return metrics.width;
}

/*
    Function for our ticker specifically
*/
function getTickerTextWidth() {
    tickerTxt = getTickerElem();
    // tickerTxt = getHiddenTickerElem();
    if (!tickerTxt) {
        return 0;
    }
    let txt = tickerTxt.innerText;

    // console.log(
    //     getComputedStyle(tickerTxt).fontWeight + " " +
    //     getComputedStyle(tickerTxt).fontSize + " " +
    //     getComputedStyle(tickerTxt).fontFamily
    // );

    let txtPixelWidth = getTextWidth(txt,
        getComputedStyle(tickerTxt).fontWeight + " " +
        getComputedStyle(tickerTxt).fontSize + " " +
        getComputedStyle(tickerTxt).fontFamily);

    return Math.ceil(txtPixelWidth);
}

function getTickerWidth() {
    return Math.ceil(document.getElementById("ticker").getBoundingClientRect().width);
}

function isTickerTextOverflow() {
    let txtWidth = getTickerTextWidth();
    let tickerBoxWidth = document.getElementById("ticker-txt").getBoundingClientRect().width;

    // return ( getTickerTextWidth() > getTickerWidth() );
    return ( txtWidth > tickerBoxWidth );
}

/*
    Returns a negative offset (ticker is scrolling to the left) for how far the ticker will scroll.
*/
function getAnimXOffset() {
    if (!isTickerTextOverflow()) {
        return 0;
    }

    let tickerTxt = document.getElementById("ticker-txt");
    const style = getComputedStyle(tickerTxt);

    let marginLeft = style.marginLeft.slice(0, -2)*1;
    
    if (!marginLeft || marginLeft < 0) {
        marginLeft = 0;
    }

    let marginRight = style.marginRight.slice(0, -2)*1;

    if (!marginRight || marginRight < 0) {
        marginRight = 0;
    }

    // the offset is for an A E S T H E T I C padding for potentially wide glyphs
    return (getTickerTextWidth() - (getTickerWidth() - (marginRight + marginLeft))) * -1 - _aestheticOffset;
}

function clearAnimClasses(elem, classes) {
    for (let i = 0; i < classes.length; ++i) {
        elem.classList.remove(classes[i]);
    }
}

function parseMatrix(matrixStr) {
    // matrix(1, 0, 0, 1, -35.6225, 0)
    // return as numeric
    let res = matrixStr.slice(7, -1).split(",");
    for (let i = 0; i < res.length; ++i) {
        res[i] = res[i] * 1.0; // coerce to floats
    }
    return {
        mat: res,
        translateX: res[4],
        translateY: res[5]
    };
}

function getScrollSpeed(givenSize) {
    // givenSize is some factor of the ticker and text size to determine how fast to scroll
    let speed = _minScrollSpeedMs + (givenSize * _scrollSpeedStepMs);
    speed = clamp(speed, _minScrollSpeedMs, _maxScrollSpeedMs);
    return speed;
}

/*
    Set up all variables and CSS rules for the target (ticker) given the current
    state of the page. Text should be updated before calling this function.
*/
function setAnim(target) {
    // set up the new x-offset target to scroll to
    const xTargetOffset = getAnimXOffset();
    target.style.setProperty("--left-translate", "translateX("+xTargetOffset+"px)");
    console.log("translateX("+xTargetOffset+"px)");

    // calculate time spent scrolling based on amount of text
    let scrollSpeed = getScrollSpeed((xTargetOffset*-1) / _pixelsPerStep);

    // remove scroll delay class
    target.classList.remove(_curScrollDelay);

    // set global scroll delay based on desired scroll time
    _curScrollDelay = getScrollDelay(scrollSpeed);

    // set the scroll delay class
    const scrollClass = getScrollDelayClass(_curScrollDelay);
    target.classList.add(scrollClass);
    console.log("Set scroll delay class to: " + scrollClass);

    // calculate real scroll time by factoring in scroll delay %'s
    let realSpeed = calcRealTranslateTime(scrollSpeed);

    // set global to be tracked by text update loop
    _updateDelay = calcUpdateDelay(realSpeed);

    console.log("Set updateDelay to "+_updateDelay);

    target.style.setProperty("--scroll-speed", realSpeed+"ms");
    console.log(realSpeed+"ms");
}

function fadeCallback(target, newText) {
    target.classList.remove("fadeout");

    // set text
    target.innerText = newText ? newText : _defaultMsg;

    setAnim(target);

    // fade in
    target.style.setProperty("--transitionFadeInInit", "translate(0px,-"+_transitionFadePx+"px)");
    target.style.setProperty("--transitionFadeInEnd", "translate(0px,0px)");
    target.classList.add("fadein");
    window.setTimeout(function(){target.classList.remove("fadein")}, _transitionFadeMs);
}

function updateTicker(newText, playTransition) {
    
    let target = getTickerElem();
    if (_cleanChars) newText = filter.clean(newText);
    if (_censorLanguage) newText = filter.censor(newText, _censorType);

    if (!newText) {
        // Don't do anything if the cleaning and censoring has cleared the whole string
        // Or if we get an invalid string anyhow
        return;
    }

    if (playTransition) {
        // play fade out, fade in by callback
        let matrix = parseMatrix(getComputedStyle(target).transform);
        const xCurOffset = matrix.translateX;
        target.style.setProperty("--transitionFadeOutInit", "translate("+xCurOffset+"px,0px)");
        target.style.setProperty("--transitionFadeOutEnd", "translate("+xCurOffset+"px,"+_transitionFadePx+"px)");
        target.classList.add("fadeout");
        // can set a _fading global here and unset in callback function
        //  this may fix the issue where long messages don't scroll fully before transitioning
        window.setTimeout(fadeCallback, _transitionFadeMs, target, newText);
    }
    
    else {
        // set text
        target.innerText = newText ? newText : _defaultMsg;

        setAnim(target);
    }
}

function handleResize() {
    if (!_resizing) {
        var _resizing = true;
        window.requestAnimationFrame(
            function() {
                setAnim(getTickerElem());
                console.log("Reset animation due to window resize.");
                _resizing = false;
            }
        )
    }
}

function getTickerElem() {
    return document.querySelector("#ticker-txt > p");
}

Array.prototype.enqueue = function(item) {
    return this.push(item);
}

Array.prototype.dequeue = function() {
    return this.shift();
}

function handleDiscordMessages(e) {
    let response = JSON.parse(e.target.response);
    if (response && response.data) {
        let msgArrayRaw = JSON.parse(response.data);
        _qLock = true;
        for (let i = 0; i < msgArrayRaw.length; ++i) {
            _queue.enqueue(JSON.parse(msgArrayRaw[i]));
        }
        _qLock = false;

        if (msgArrayRaw.length > 0)
            console.log(_queue.length + " message(s) now in queue");
    }
}

function pollDiscord() {
    let req = new XMLHttpRequest();
    req.addEventListener("load", handleDiscordMessages);
    req.open("GET", "http://localhost:"+_localPort+"/queue/");
    req.send();
    window.setTimeout(pollDiscord, _pollRateMs);
}

function processMsgQueue() {
    if (!_qLock && _queue.length > 0) {
        if (_scrolledOnce) {
            _scrolledOnce = false;
        }

        updateTicker(_queue.dequeue().Message, true);

        window.setTimeout(processMsgQueue, _updateDelay);
    }
    else {
        if (!_scrolledOnce) {
            window.setTimeout(
                function() {
                    _scrolledOnce = true;
                    _updateDelay = _minScrollSpeedMs;
                },
                _updateDelay
            );
        }

        window.setTimeout(processMsgQueue, _updateDelay);
    }
}

function setup() {
    if (!_initialized) {
        getCSSRule(".fadeout").style.animationDuration = _transitionFadeMs+"ms";
        getCSSRule(".fadein").style.animationDuration = _transitionFadeMs+"ms";
        var _initialized = true;

        // setTickerAnim(_defaultMsg, false);
        // Can't set the animation straight away due to bug in Chrome
        // Canvas reports incorrect text width if called too quickly on page load
        window.setTimeout(updateTicker, 5, _defaultMsg, false);
        pollDiscord();
        processMsgQueue();
    }
    else {
        console.warn("Tried to set up ticker more than once (function call ignored)");
    }
}

