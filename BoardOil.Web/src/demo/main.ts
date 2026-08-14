import { createApp } from 'vue';
import { createPinia } from 'pinia';
import { configureBoardApiFactory } from '../shared/api/boardApi';
import { configureBoardRealtimeFactory } from '../board/realtime/boardRealtime';
import DemoApp from './DemoApp.vue';
import { createDemoBoardApi } from './demoBoardApi';
import { createDemoRealtime } from './demoRealtime';
import { installDemoTheme } from './demoTheme';
import { demoRouter } from './router';
import '../style.css';

configureBoardApiFactory(createDemoBoardApi);
configureBoardRealtimeFactory(createDemoRealtime);
installDemoTheme();

const pinia = createPinia();

const app = createApp(DemoApp);
app.use(pinia);
app.use(demoRouter);
app.mount('#app');
