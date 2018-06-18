var Replacement = function(findStr, replaceWith) {
    this.str = findStr;
    this.replaceStr = replaceWith;
}

filter = {
    CENSOR_TYPE: {
        DELETE: 0,
        BLEEP: 1
    },

    // should just have Regexes, here, it's bad for performance to make them in the loop
    badStr: [
        new Replacement("\n", ""),
        new Replacement("^\\s+$", ""),     // Replace all-whitespace
        new Replacement("<:.+:.+>", "😃")  // Replace Discord custom emoji with smiley face
    ],

    badWords: ["fuck", "shit", "nintendo switch"],

    clean: function(text) {
        for (let i = 0; i < this.badStr.length; ++i) {
            text = text.replace(
                new RegExp(this.badStr[i].str, "gi"),
                this.badStr[i].replaceStr
            );
        }
        return text;
    },

    censor: function(text, type) {
        for (let i = 0; i < this.badWords.length; ++i) {
            if (type === this.CENSOR_TYPE.BLEEP) {
                let stars = this.badWords[i].replace(/./g, "*");
                text = text.replace(new RegExp(this.badWords[i], "gi"), stars);
            }
            else {
                text = text.replace(new RegExp(this.badWords[i], "gi"), " ");
            }
        }
        return text;
    }
}
