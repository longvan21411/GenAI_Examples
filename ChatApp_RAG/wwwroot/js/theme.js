(function () {
    window.theme = {
        init: function () {
            try {
                const stored = localStorage.getItem('site-theme');
                if (stored === 'dark') {
                    document.documentElement.classList.add('dark');
                    return true;
                }

                if (stored === 'light') {
                    document.documentElement.classList.remove('dark');
                    return false;
                }

                // follow OS preference
                const preferDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
                if (preferDark) document.documentElement.classList.add('dark');
                return preferDark;
            }
            catch (e) {
                return false;
            }
        },
        toggle: function () {
            try {
                const isDark = document.documentElement.classList.toggle('dark');
                localStorage.setItem('site-theme', isDark ? 'dark' : 'light');
                return isDark;
            }
            catch (e) {
                return false;
            }
        }
    };
})();
