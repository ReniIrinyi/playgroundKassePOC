<template>
  <q-layout view="hHh lpR fFf">
    <q-header elevated>
      <q-toolbar>

        <q-icon name="img:/green.svg" class="q-mr-sm" size="36px"/>
        <q-space/>
        <q-btn style="color: #21645f" :icon="online ? 'wifi' : 'wifi_off'" :title="online ? 'Online' : 'Offline'" dense flat/>
        <q-btn dense flat style="color: #21645f" icon="settings" title="Einstellungen"/>
      </q-toolbar>
    </q-header>
    <q-page-container>
      <router-view/>
    </q-page-container>
  </q-layout>
</template>

<script lang="ts" setup>
import {onBeforeUnmount, onMounted, ref} from 'vue'

const online = ref(navigator.onLine)

function update() {
  online.value = navigator.onLine
}

onMounted(() => {
  window.addEventListener('online', update)
  window.addEventListener('offline', update)
})
onBeforeUnmount(() => {
  window.removeEventListener('online', update)
  window.removeEventListener('offline', update)
})
</script>
