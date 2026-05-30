import { createApp } from 'vue';
import { createPinia } from 'pinia';
import App from './App.vue';
import { router } from './router';
import { useThemeStore } from './shared/stores/themeStore';
import './style.css';

const pinia = createPinia();
useThemeStore(pinia).initialize();

const app = createApp(App);

app.use(pinia);
app.use(router);
app.mount('#app');
