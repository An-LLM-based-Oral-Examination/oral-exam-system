import { useEffect, useRef } from 'react'
import * as THREE from 'three'

interface PracticeHeroThreeSceneProps {
  active?: boolean
}

interface OrbitNode {
  mesh: THREE.Mesh
  phase: number
  baseScale: number
}

function createEllipseLine(radiusX: number, radiusY: number, color: number, opacity: number) {
  const points: THREE.Vector3[] = []

  for (let index = 0; index < 120; index += 1) {
    const angle = (index / 120) * Math.PI * 2
    points.push(new THREE.Vector3(Math.cos(angle) * radiusX, Math.sin(angle) * radiusY, 0))
  }

  return new THREE.LineLoop(
    new THREE.BufferGeometry().setFromPoints(points),
    new THREE.LineBasicMaterial({
      color,
      opacity,
      transparent: true,
      depthWrite: false,
    }),
  )
}

export function PracticeHeroThreeScene({ active = false }: PracticeHeroThreeSceneProps) {
  const mountRef = useRef<HTMLDivElement>(null)
  const activeRef = useRef(active)

  useEffect(() => {
    activeRef.current = active
  }, [active])

  useEffect(() => {
    const mount = mountRef.current
    if (!mount) return

    const scene = new THREE.Scene()
    const camera = new THREE.PerspectiveCamera(34, 1, 0.1, 50)
    camera.position.set(0, 0.08, 7.4)

    let renderer: THREE.WebGLRenderer

    try {
      renderer = new THREE.WebGLRenderer({
        alpha: true,
        antialias: true,
        powerPreference: 'high-performance',
      })
    } catch {
      return
    }

    renderer.setClearColor(0x000000, 0)
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.6))
    renderer.outputColorSpace = THREE.SRGBColorSpace
    renderer.toneMapping = THREE.ACESFilmicToneMapping
    renderer.toneMappingExposure = 1.06
    renderer.domElement.setAttribute('aria-hidden', 'true')
    renderer.domElement.style.display = 'block'
    renderer.domElement.style.width = '100%'
    renderer.domElement.style.height = '100%'
    mount.appendChild(renderer.domElement)

    const ambientLight = new THREE.HemisphereLight(0xe9fdff, 0x06152e, 1.65)
    const keyLight = new THREE.DirectionalLight(0xcdf9ff, 3.2)
    const blueLight = new THREE.PointLight(0x3777ff, 10, 10, 1.8)
    const cyanLight = new THREE.PointLight(0x53f2ff, 12, 9, 1.7)
    keyLight.position.set(3, 4.5, 5)
    blueLight.position.set(-2.3, -0.8, 2.5)
    cyanLight.position.set(2, 1.5, 3)
    scene.add(ambientLight, keyLight, blueLight, cyanLight)

    const rootGroup = new THREE.Group()
    rootGroup.position.set(0.45, 0, 0)
    scene.add(rootGroup)

    const waveformGroup = new THREE.Group()
    waveformGroup.position.set(0, -0.08, -0.72)
    rootGroup.add(waveformGroup)

    const barGeometry = new THREE.BoxGeometry(0.075, 1, 0.075)
    const waveformBars: THREE.Mesh[] = []
    for (let index = 0; index < 19; index += 1) {
      const distanceFromCenter = Math.abs(index - 9) / 9
      const barMaterial = new THREE.MeshStandardMaterial({
        color: index % 2 === 0 ? 0x4de8f5 : 0x5e8cff,
        emissive: index % 2 === 0 ? 0x158da9 : 0x244cb5,
        emissiveIntensity: 0.72,
        metalness: 0.42,
        roughness: 0.28,
        transparent: true,
        opacity: 0.74 - distanceFromCenter * 0.25,
      })
      const bar = new THREE.Mesh(barGeometry, barMaterial)
      bar.position.x = (index - 9) * 0.25
      bar.scale.y = 0.18 + (1 - distanceFromCenter) * 0.34
      waveformGroup.add(bar)
      waveformBars.push(bar)
    }

    const coreGroup = new THREE.Group()
    coreGroup.position.z = 0.12
    rootGroup.add(coreGroup)

    const innerCoreMaterial = new THREE.MeshPhysicalMaterial({
      color: 0x1f6fe5,
      emissive: 0x1249a8,
      emissiveIntensity: 0.72,
      metalness: 0.38,
      roughness: 0.22,
      clearcoat: 0.85,
      clearcoatRoughness: 0.16,
    })
    const innerCore = new THREE.Mesh(new THREE.IcosahedronGeometry(0.49, 4), innerCoreMaterial)
    coreGroup.add(innerCore)

    const shellMaterial = new THREE.MeshPhysicalMaterial({
      color: 0x77f4ff,
      emissive: 0x1da4bf,
      emissiveIntensity: 0.24,
      metalness: 0.04,
      roughness: 0.12,
      clearcoat: 1,
      clearcoatRoughness: 0.08,
      transparent: true,
      opacity: 0.23,
      transmission: 0.2,
      thickness: 0.2,
      depthWrite: false,
    })
    const shell = new THREE.Mesh(new THREE.IcosahedronGeometry(0.68, 3), shellMaterial)
    shell.renderOrder = 3
    coreGroup.add(shell)

    const micMaterial = new THREE.MeshStandardMaterial({
      color: 0x071a3a,
      emissive: 0x0c2f73,
      emissiveIntensity: 0.42,
      metalness: 0.68,
      roughness: 0.22,
    })
    const micHead = new THREE.Mesh(new THREE.CapsuleGeometry(0.16, 0.28, 8, 20), micMaterial)
    micHead.position.y = 0.07
    micHead.position.z = 0.5
    coreGroup.add(micHead)

    const micStem = new THREE.Mesh(new THREE.CylinderGeometry(0.035, 0.045, 0.24, 16), micMaterial)
    micStem.position.set(0, -0.36, 0.5)
    coreGroup.add(micStem)

    const micBase = new THREE.Mesh(new THREE.CylinderGeometry(0.18, 0.24, 0.05, 24), micMaterial)
    micBase.position.set(0, -0.49, 0.5)
    coreGroup.add(micBase)

    const ringMaterial = new THREE.MeshBasicMaterial({
      color: 0x8cf8ff,
      opacity: 0.58,
      transparent: true,
      depthWrite: false,
    })
    const coreRing = new THREE.Mesh(new THREE.TorusGeometry(0.82, 0.014, 10, 80), ringMaterial)
    coreRing.rotation.x = Math.PI / 2
    coreGroup.add(coreRing)

    const orbitOneGroup = new THREE.Group()
    orbitOneGroup.rotation.set(0.68, 0.22, -0.2)
    const orbitOne = createEllipseLine(1.75, 0.72, 0x57e9f5, 0.48)
    orbitOneGroup.add(orbitOne)
    rootGroup.add(orbitOneGroup)

    const orbitTwoGroup = new THREE.Group()
    orbitTwoGroup.rotation.set(-0.5, -0.24, 0.78)
    const orbitTwo = createEllipseLine(1.58, 0.64, 0x7089ff, 0.35)
    orbitTwoGroup.add(orbitTwo)
    rootGroup.add(orbitTwoGroup)

    const nodeMaterial = new THREE.MeshStandardMaterial({
      color: 0xc7fbff,
      emissive: 0x4ad9eb,
      emissiveIntensity: 0.85,
      metalness: 0.22,
      roughness: 0.18,
    })
    const orbitNodes: OrbitNode[] = []

    const addOrbitNodes = (
      parent: THREE.Group,
      radiusX: number,
      radiusY: number,
      count: number,
    ) => {
      for (let index = 0; index < count; index += 1) {
        const angle = (index / count) * Math.PI * 2 + 0.3
        const baseScale = index % 2 === 0 ? 1 : 0.7
        const node = new THREE.Mesh(new THREE.SphereGeometry(0.055, 14, 14), nodeMaterial)
        node.position.set(Math.cos(angle) * radiusX, Math.sin(angle) * radiusY, 0)
        node.scale.setScalar(baseScale)
        parent.add(node)
        orbitNodes.push({ mesh: node, phase: index * 0.9, baseScale })
      }
    }

    addOrbitNodes(orbitOneGroup, 1.75, 0.72, 5)
    addOrbitNodes(orbitTwoGroup, 1.58, 0.64, 4)

    const particlesGeometry = new THREE.BufferGeometry()
    const particleCount = 36
    const particlePositions = new Float32Array(particleCount * 3)
    for (let index = 0; index < particleCount; index += 1) {
      particlePositions[index * 3] = (Math.random() - 0.5) * 5
      particlePositions[index * 3 + 1] = (Math.random() - 0.5) * 2.8
      particlePositions[index * 3 + 2] = (Math.random() - 0.5) * 2 - 1
    }
    particlesGeometry.setAttribute('position', new THREE.BufferAttribute(particlePositions, 3))
    const particles = new THREE.Points(
      particlesGeometry,
      new THREE.PointsMaterial({
        color: 0x71eaf8,
        size: 0.035,
        opacity: 0.48,
        transparent: true,
        blending: THREE.AdditiveBlending,
        depthWrite: false,
      }),
    )
    rootGroup.add(particles)

    const pointerTarget = new THREE.Vector2()
    const pointerCurrent = new THREE.Vector2()
    const motionPreference = window.matchMedia('(prefers-reduced-motion: reduce)')
    let reduceMotion = motionPreference.matches
    let isVisible = document.visibilityState !== 'hidden'
    let isInViewport = true
    const startedAt = performance.now()
    const orbitOneRotation = orbitOneGroup.rotation.clone()
    const orbitTwoRotation = orbitTwoGroup.rotation.clone()

    const resetPose = () => {
      pointerTarget.set(0, 0)
      pointerCurrent.set(0, 0)
      rootGroup.position.set(0.45, 0, 0)
      rootGroup.rotation.set(0, 0, 0)
      coreGroup.position.set(0, 0, 0.12)
      orbitOneGroup.rotation.copy(orbitOneRotation)
      orbitTwoGroup.rotation.copy(orbitTwoRotation)
      waveformBars.forEach((bar, index) => {
        const distanceFromCenter = Math.abs(index - 9) / 9
        bar.scale.y = 0.18 + (1 - distanceFromCenter) * 0.34
      })
    }

    const renderFrame = (time = performance.now()) => {
      const elapsed = (time - startedAt) * 0.001

      if (!reduceMotion && isVisible && isInViewport) {
        const activity = activeRef.current ? 1.75 : 0.72
        pointerCurrent.lerp(pointerTarget, 0.055)
        rootGroup.position.x = 0.45 + pointerCurrent.x * 0.13
        rootGroup.position.y = pointerCurrent.y * -0.08
        rootGroup.rotation.y = pointerCurrent.x * 0.13
        rootGroup.rotation.x = pointerCurrent.y * -0.075

        coreGroup.position.y = Math.sin(elapsed * 1.05) * 0.055
        coreGroup.rotation.y = elapsed * 0.12
        innerCore.scale.setScalar(1 + Math.sin(elapsed * 2.1) * 0.035 * activity)
        shell.rotation.y = elapsed * -0.09
        coreRing.rotation.z = elapsed * 0.22
        orbitOneGroup.rotation.z = orbitOneRotation.z + elapsed * 0.12
        orbitTwoGroup.rotation.z = orbitTwoRotation.z - elapsed * 0.095
        particles.rotation.y = elapsed * 0.025

        waveformBars.forEach((bar, index) => {
          const centerWeight = 1 - Math.abs(index - 9) / 11
          const wave = Math.sin(elapsed * (2.2 + activity) + index * 0.72)
          const secondaryWave = Math.sin(elapsed * 1.35 - index * 0.31)
          bar.scale.y = Math.max(
            0.12,
            0.19 + centerWeight * 0.2 + Math.abs(wave + secondaryWave * 0.45) * 0.13 * activity,
          )
        })

        orbitNodes.forEach(({ mesh, phase, baseScale }) => {
          mesh.scale.setScalar(baseScale * (1 + Math.sin(elapsed * 1.7 + phase) * 0.16))
        })

        innerCoreMaterial.emissiveIntensity =
          (activeRef.current ? 1.05 : 0.68) + Math.sin(elapsed * 2.4) * 0.08
      }

      renderer.render(scene, camera)
    }

    const syncAnimation = () => {
      const shouldAnimate = !reduceMotion && isVisible && isInViewport
      renderer.setAnimationLoop(shouldAnimate ? renderFrame : null)

      if (!shouldAnimate) {
        resetPose()
        renderFrame()
      }
    }

    const handlePointerMove = (event: PointerEvent) => {
      if (reduceMotion) return
      const rect = mount.getBoundingClientRect()
      if (rect.width === 0 || rect.height === 0) return

      const relativeX = (event.clientX - rect.left) / rect.width
      const relativeY = (event.clientY - rect.top) / rect.height
      if (relativeX < -0.25 || relativeX > 1.25 || relativeY < -0.4 || relativeY > 1.4) {
        pointerTarget.set(0, 0)
        return
      }

      pointerTarget.set((relativeX - 0.5) * 2, (relativeY - 0.5) * 2)
    }

    const resizeScene = () => {
      const { width, height } = mount.getBoundingClientRect()
      if (width === 0 || height === 0) return

      renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.6))
      renderer.setSize(width, height, false)
      camera.aspect = width / height
      camera.updateProjectionMatrix()
      renderFrame()
    }

    const handleVisibilityChange = () => {
      isVisible = document.visibilityState !== 'hidden'
      syncAnimation()
    }

    const handleMotionPreference = (event: MediaQueryListEvent) => {
      reduceMotion = event.matches
      syncAnimation()
    }

    const resizeObserver = new ResizeObserver(resizeScene)
    resizeObserver.observe(mount)
    const intersectionObserver = new IntersectionObserver(
      ([entry]) => {
        isInViewport = entry.isIntersecting
        syncAnimation()
      },
      { threshold: 0.05 },
    )
    intersectionObserver.observe(mount)

    window.addEventListener('pointermove', handlePointerMove, { passive: true })
    document.addEventListener('visibilitychange', handleVisibilityChange)
    motionPreference.addEventListener('change', handleMotionPreference)

    resizeScene()
    syncAnimation()

    return () => {
      renderer.setAnimationLoop(null)
      resizeObserver.disconnect()
      intersectionObserver.disconnect()
      window.removeEventListener('pointermove', handlePointerMove)
      document.removeEventListener('visibilitychange', handleVisibilityChange)
      motionPreference.removeEventListener('change', handleMotionPreference)

      const geometries = new Set<THREE.BufferGeometry>()
      const materials = new Set<THREE.Material>()
      scene.traverse((object) => {
        const renderable = object as THREE.Object3D & {
          geometry?: THREE.BufferGeometry
          material?: THREE.Material | THREE.Material[]
        }
        if (renderable.geometry) geometries.add(renderable.geometry)
        if (Array.isArray(renderable.material)) {
          renderable.material.forEach((material) => materials.add(material))
        } else if (renderable.material) {
          materials.add(renderable.material)
        }
      })

      geometries.forEach((geometry) => geometry.dispose())
      materials.forEach((material) => material.dispose())
      renderer.dispose()
      renderer.forceContextLoss()

      if (renderer.domElement.parentElement === mount) {
        mount.removeChild(renderer.domElement)
      }
    }
  }, [])

  return <div ref={mountRef} className="size-full" aria-hidden="true" />
}
