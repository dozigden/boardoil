import { createApp } from 'vue';
import { createPinia } from 'pinia';
import App from './App.vue';
import { router } from './router';
import { installFrontendErrorReporting } from './shared/errors/clientErrorReporter';
import { useThemeStore } from './shared/stores/themeStore';
import './style.css';

const pinia = createPinia();
useThemeStore(pinia).initialize();

const app = createApp(App);

app.use(pinia);
app.use(router);
installFrontendErrorReporting(app, router);
app.mount('#app');
